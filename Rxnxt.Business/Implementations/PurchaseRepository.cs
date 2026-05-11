using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Rxnxt.Business.Data;
using Rxnxt.Business.DTOs;
using Rxnxt.Business.Interfaces;
using Rxnxt.Domain.Models;
using System.Data;

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

                var supplierMasterUniqueId = (request.SupplierMasterUniqueId ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(supplierMasterUniqueId))
                    return new PurchaseResult { Success = false, Message = "Supplier is required" };

                var supplierExists = await _context.SupplierMasters.AsNoTracking().AnyAsync(s => s.UniqueID == supplierMasterUniqueId);
                if (!supplierExists)
                    return new PurchaseResult { Success = false, Message = "Supplier not found" };

                decimal subtotal = 0m;
                decimal discount = 0m;
                decimal tax = 0m;

                if (request.RefDate == default)
                    return new PurchaseResult { Success = false, Message = "Ref Date is required" };

                using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                static string NormalizeUnit(string? u)
                {
                    var s = (u ?? string.Empty).Trim().ToUpperInvariant();
                    if (s == "PCS") return "PCS";
                    if (s == "STRIP") return "STRIP";

                    if (s == "TABLET" || s == "TAB" || s == "TABS" || s == "TB" || s == "TBL") return "PCS";
                    if (s == "CAP" || s == "CAPS" || s == "CAPSULE" || s == "CAPSULES") return "PCS";
                    if (s == "PACK" || s == "PK" || s == "PKT" || s == "BOX") return "STRIP";
                    if (s == "STR" || s == "STP") return "STRIP";
                    if (s == "UNIT" || s == "PIECE" || s == "PIECES") return "PCS";

                    return "STRIP";
                }

                var grnDate = DateTime.Now;
                var year = grnDate.Year;
                var yearStart = new DateTime(year, 1, 1);
                var yearEnd = yearStart.AddYears(1);

                var lastForYear = await _context.GrnHeaders
                    .AsNoTracking()
                    .Where(h => h.GRNDate >= yearStart && h.GRNDate < yearEnd && h.GRNNo.StartsWith("PUR-"))
                    .OrderByDescending(h => h.ID)
                    .Select(h => h.GRNNo)
                    .FirstOrDefaultAsync();

                var nextNo = 1;
                if (!string.IsNullOrWhiteSpace(lastForYear))
                {
                    var parts = lastForYear.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length == 2 && int.TryParse(parts[1], out var lastN) && lastN > 0)
                        nextNo = lastN + 1;
                }

                var grnNo = $"PUR-{nextNo}";
                var grnUniqueId = Guid.NewGuid().ToString();

                var header = new GrnHeaderRow
                {
                    UniqueID = grnUniqueId,
                    GRNNo = grnNo,
                    GRNDate = grnDate,
                    GRNType = "PURCHASE",
                    SupplierID = supplierMasterUniqueId,
                    RefNumber = supplierInvoiceNo,
                    RefDate = request.RefDate.Date,
                    ActiveStatus = true,
                    CreatedBy = "ADMIN",
                    CreatedDate = grnDate,
                    TenantId = null
                };

                _context.GrnHeaders.Add(header);
                await _context.SaveChangesAsync();

                foreach (var item in request.Items)
                {
                    var productUniqueId = (item.ProductUniqueId ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(productUniqueId))
                        return new PurchaseResult { Success = false, Message = "Invalid product" };

                    var productExists = await _context.ProductMasters.AsNoTracking().AnyAsync(p => p.UniqueID == productUniqueId);
                    if (!productExists)
                        return new PurchaseResult { Success = false, Message = "Invalid product" };

                    var unitNorm = NormalizeUnit(item.Unit);
                    var unitId = await ResolveUnitMasterUniqueIdAsync(unitNorm);

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

                    var detail = new GrnDetailRow
                    {
                        UniqueID = Guid.NewGuid().ToString(),
                        GRNID = grnUniqueId,
                        ProductID = productUniqueId,
                        BatchNumber = string.IsNullOrWhiteSpace(item.BatchNumber) ? null : item.BatchNumber.Trim(),
                        ExpiryDate = item.ExpiryDate?.Date,
                        UnitID = unitId,
                        Qty = qty,
                        PurchasePrice = rate,
                        MRP = item.Mrp,
                        ItemDiscPerc = item.DiscountPercent,
                        ItemDiscAmount = lineDisc,
                        TenantId = null
                    };

                    _context.GrnDetails.Add(detail);

                    await UpsertProductStockAsync(productUniqueId, detail.BatchNumber, detail.ExpiryDate, qty);
                }

                await _context.SaveChangesAsync();

                var additionalDiscount = request.AdditionalDiscountAmount;
                if (additionalDiscount < 0) additionalDiscount = 0;

                var gross = Math.Max(0, subtotal - additionalDiscount);
                var roundOff = request.RoundOff;

                var grand = gross + tax + roundOff;
                if (grand < 0) grand = 0;

                header.BillAmount = grand;
                header.TaxAmount = tax;
                header.DiscountAmount = discount + additionalDiscount;
                header.TotalBeforeRoundOff = subtotal;
                header.ExtraLess = additionalDiscount;
                header.ExtraAdd = roundOff;

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return new PurchaseResult
                {
                    Success = true,
                    Message = $"Purchase saved (GRN: {grnNo})",
                    PurchaseId = null,
                    SupplierInvoiceNo = supplierInvoiceNo
                };
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message;
                var msg = inner == null ? ex.Message : $"{ex.Message} | Inner: {inner}";
                return new PurchaseResult { Success = false, Message = msg };
            }
        }

        private async Task<string?> ResolveUnitMasterUniqueIdAsync(string unitNorm)
        {
            if (string.IsNullOrWhiteSpace(unitNorm)) return null;

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var dbTx = _context.Database.CurrentTransaction?.GetDbTransaction();

            string? nameColumn = null;
            await using (var cmdCols = conn.CreateCommand())
            {
                cmdCols.Transaction = dbTx;
                cmdCols.CommandText = @"SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'UnitMaster'";

                await using var rdr = await cmdCols.ExecuteReaderAsync();
                var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (await rdr.ReadAsync())
                {
                    var c = rdr.GetString(0);
                    if (!string.IsNullOrWhiteSpace(c)) cols.Add(c);
                }

                var candidates = new[] { "UnitName", "UOMName", "UomName", "Name" };
                nameColumn = candidates.FirstOrDefault(cols.Contains);
            }

            if (string.IsNullOrWhiteSpace(nameColumn))
                return null;

            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = dbTx;
                cmd.CommandText = $@"SELECT TOP (1) [UniqueID]
FROM [dbo].[UnitMaster]
WHERE UPPER(LTRIM(RTRIM([{nameColumn}]))) = @u";

                var p = cmd.CreateParameter();
                p.ParameterName = "@u";
                p.Value = unitNorm.ToUpperInvariant();
                cmd.Parameters.Add(p);

                var result = await cmd.ExecuteScalarAsync();
                var s = result == null ? null : Convert.ToString(result);
                return string.IsNullOrWhiteSpace(s) ? null : s;
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
