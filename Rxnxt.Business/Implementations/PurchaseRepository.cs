using Microsoft.EntityFrameworkCore;
using Rxnxt.Business.Data;
using Rxnxt.Business.DTOs;
using Rxnxt.Business.Interfaces;
using Rxnxt.Domain.Models;

namespace Rxnxt.Business.Implementations
{
    public sealed class PurchaseRepository : IPurchaseRepository
    {
        private readonly PharmacyDbContext _context;

        public PurchaseRepository(PharmacyDbContext context)
        {
            _context = context;
        }

        public async Task<PurchaseResult> CompletePurchaseAsync(CompletePurchaseRequest request)
        {
            try
            {
                if (request.Items == null || request.Items.Count == 0)
                    return new PurchaseResult { Success = false, Message = "No items in the purchase" };

                var supplierInvoiceNo = (request.SupplierInvoiceNo ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(supplierInvoiceNo))
                    return new PurchaseResult { Success = false, Message = "Supplier Invoice No is required" };

                Supplier? supplier = null;
                if (request.SupplierId.HasValue)
                {
                    supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId.Value);
                    if (supplier == null)
                        return new PurchaseResult { Success = false, Message = "Supplier not found" };
                }
                else
                {
                    var name = (request.SupplierName ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(name))
                        return new PurchaseResult { Success = false, Message = "Supplier is required" };

                    supplier = new Supplier { Name = name, CreatedDate = DateTime.Now };
                    _context.Suppliers.Add(supplier);
                    await _context.SaveChangesAsync();
                }

                var alreadyExists = await _context.Purchases.AsNoTracking().AnyAsync(p => p.SupplierId == supplier.Id && p.SupplierInvoiceNo == supplierInvoiceNo);
                if (alreadyExists)
                    return new PurchaseResult { Success = false, Message = "This Supplier Invoice No already exists for this supplier" };

                decimal subtotal = 0m;
                decimal discount = 0m;
                decimal tax = 0m;

                if (request.RefDate == default)
                    return new PurchaseResult { Success = false, Message = "Ref Date is required" };

                var purchase = new Purchase
                {
                    SupplierId = supplier.Id,
                    SupplierInvoiceNo = supplierInvoiceNo,
                    InvoiceDate = request.InvoiceDate.Date,
                    RefDate = request.RefDate.Date,
                    DueDate = request.DueDate?.Date,
                    CreatedDate = DateTime.Now
                };

                using var tx = await _context.Database.BeginTransactionAsync();

                _context.Purchases.Add(purchase);
                await _context.SaveChangesAsync();

                foreach (var item in request.Items)
                {
                    if (item.ProductId == Guid.Empty)
                        return new PurchaseResult { Success = false, Message = "Invalid product" };

                    var qty = item.Qty;
                    if (qty <= 0)
                        return new PurchaseResult { Success = false, Message = $"Invalid quantity for {item.ProductName}" };

                    var rate = item.PurchaseRate;
                    if (rate < 0) rate = 0;

                    var lineTaxable = qty * rate;
                    var lineDisc = lineTaxable * (item.DiscountPercent / 100m);
                    var afterDisc = Math.Max(0, lineTaxable - lineDisc);

                    var gstPct = item.GstPercent;
                    if (gstPct < 0) gstPct = 0;

                    var lineTax = afterDisc * (gstPct / 100m);
                    var lineTotal = afterDisc + lineTax;

                    subtotal += afterDisc;
                    discount += lineDisc;
                    tax += lineTax;

                    var cgst = gstPct / 2m;
                    var sgst = gstPct / 2m;

                    var row = new PurchaseItem
                    {
                        PurchaseId = purchase.Id,
                        ProductId = item.ProductId,
                        ProductName = (item.ProductName ?? string.Empty).Trim(),
                        BatchNumber = string.IsNullOrWhiteSpace(item.BatchNumber) ? null : item.BatchNumber.Trim(),
                        ExpiryDate = item.ExpiryDate?.Date,
                        Qty = qty,
                        PurchaseRate = rate,
                        Mrp = item.Mrp,
                        DiscountPercent = item.DiscountPercent,
                        DiscountAmount = lineDisc,
                        GstPercent = gstPct,
                        CgstPercent = cgst,
                        SgstPercent = sgst,
                        TaxAmount = lineTax,
                        LineTotal = lineTotal
                    };

                    _context.PurchaseItems.Add(row);

                    await UpsertProductStockAsync(item.ProductId.ToString(), row.BatchNumber, row.ExpiryDate, qty);
                }

                await _context.SaveChangesAsync();

                var additionalDiscount = request.AdditionalDiscountAmount;
                if (additionalDiscount < 0) additionalDiscount = 0;

                var gross = Math.Max(0, subtotal - additionalDiscount);
                var roundOff = request.RoundOff;

                var grand = gross + tax + roundOff;
                if (grand < 0) grand = 0;

                decimal paid = 0m;
                if (request.Payments != null && request.Payments.Count > 0)
                {
                    foreach (var p in request.Payments)
                    {
                        if (p.Amount <= 0) continue;
                        paid += p.Amount;
                        _context.PurchasePayments.Add(new PurchasePayment
                        {
                            PurchaseId = purchase.Id,
                            Method = (p.Method ?? "Cash").Trim(),
                            ReferenceNo = string.IsNullOrWhiteSpace(p.ReferenceNo) ? null : p.ReferenceNo.Trim(),
                            Amount = p.Amount
                        });
                    }

                    await _context.SaveChangesAsync();
                }

                if (paid > grand) paid = grand;
                var balance = Math.Max(0, grand - paid);

                purchase.Subtotal = subtotal;
                purchase.DiscountAmount = discount + additionalDiscount;
                purchase.TaxAmount = tax;
                purchase.RoundOff = roundOff;
                purchase.GrandTotal = grand;
                purchase.PaidAmount = paid;
                purchase.BalanceAmount = balance;

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return new PurchaseResult
                {
                    Success = true,
                    Message = "Purchase saved",
                    PurchaseId = purchase.Id,
                    SupplierInvoiceNo = purchase.SupplierInvoiceNo
                };
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message;
                var msg = inner == null ? ex.Message : $"{ex.Message} | Inner: {inner}";
                return new PurchaseResult { Success = false, Message = msg };
            }
        }

        private async Task UpsertProductStockAsync(string productId, string? batchNumber, DateTime? expiryDate, decimal qtyToAdd)
        {
            static string NormalizeBatch(string? b) => (b ?? string.Empty).Trim();
            var batchNorm = NormalizeBatch(batchNumber);

            ProductStockRow? row = null;
            if (expiryDate.HasValue)
            {
                var exp = expiryDate.Value.Date;
                row = await _context.ProductStocks.FirstOrDefaultAsync(ps =>
                    ps.ProductID == productId &&
                    (ps.BatchNumber ?? string.Empty) == batchNorm &&
                    ps.ExpiryDate.HasValue &&
                    ps.ExpiryDate.Value.Date == exp);
            }

            if (row == null)
            {
                row = new ProductStockRow
                {
                    ProductID = productId,
                    BatchNumber = string.IsNullOrWhiteSpace(batchNorm) ? null : batchNorm,
                    ExpiryDate = expiryDate?.Date,
                    PackQty = qtyToAdd
                };
                _context.ProductStocks.Add(row);
            }
            else
            {
                row.PackQty = (row.PackQty ?? 0m) + qtyToAdd;
            }
        }

        public async Task<Purchase?> GetByIdAsync(int id)
        {
            return await _context.Purchases
                .AsNoTracking()
                .Include(p => p.Supplier)
                .Include(p => p.Items)
                .Include(p => p.Payments)
                .FirstOrDefaultAsync(p => p.Id == id);
        }
    }
}
