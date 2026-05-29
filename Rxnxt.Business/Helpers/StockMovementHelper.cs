using Rxnxt.Business.Data;

namespace Rxnxt.Business.Helpers;

public static class StockMovementHelper
{
    public static StockMovementRow BuildMovement(
        string productID,
        int? productStockID,
        string? batchNumber,
        DateTime? expiryDate,
        decimal openingBalance,
        decimal baseQtyDelta,
        string direction,
        string movementType,
        decimal? transactionQty = null,
        string? transactionUOMID = null,
        string? baseUOMID = null,
        decimal? conversionFactor = null,
        string? referenceType = null,
        string? referenceID = null,
        string? referenceLineID = null,
        string? referenceNo = null,
        string? remarks = null,
        decimal? mrp = null,
        string? unitID = null,
        string? packTypeID = null,
        string? tenantId = null,
        string? createdBy = null)
    {
        var expectedClosing = openingBalance + baseQtyDelta;

        return new StockMovementRow
        {
            UniqueID = Guid.NewGuid().ToString(),
            ProductID = productID,
            ProductStockID = productStockID?.ToString(),
            BatchNumber = batchNumber,
            ExpiryDate = expiryDate,
            MRP = mrp,
            UnitID = unitID,
            PackTypeID = packTypeID,
            Direction = direction,
            MovementType = movementType,
            TransactionQty = transactionQty ?? Math.Abs(baseQtyDelta),
            TransactionUOMID = transactionUOMID,
            BaseUOMID = baseUOMID,
            ConversionFactor = conversionFactor,
            BaseQty = baseQtyDelta,
            OpeningBalance = openingBalance,
            ExpectedClosingBalance = expectedClosing,
            ClosingBalance = expectedClosing,
            ReferenceType = referenceType,
            ReferenceID = referenceID,
            ReferenceLineID = referenceLineID,
            ReferenceNo = referenceNo,
            Remarks = remarks,
            TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId,
            CreatedBy = createdBy ?? "POS",
            CreatedDate = DateTime.Now
        };
    }

    public static StockMovementRow BuildMovement(
        ProductStockRow stockBefore,
        decimal baseQtyDelta,
        string direction,
        string movementType,
        decimal? transactionQty = null,
        string? transactionUOMID = null,
        string? baseUOMID = null,
        decimal? conversionFactor = null,
        string? referenceType = null,
        string? referenceID = null,
        string? referenceLineID = null,
        string? referenceNo = null,
        string? remarks = null,
        string? unitID = null,
        string? packTypeID = null,
        string? tenantId = null,
        string? createdBy = null)
    {
        return BuildMovement(
            productID: stockBefore.ProductID,
            productStockID: stockBefore.ID,
            batchNumber: stockBefore.BatchNumber,
            expiryDate: stockBefore.ExpiryDate,
            openingBalance: stockBefore.PackQty ?? 0m,
            baseQtyDelta: baseQtyDelta,
            direction: direction,
            movementType: movementType,
            transactionQty: transactionQty,
            transactionUOMID: transactionUOMID,
            baseUOMID: baseUOMID,
            conversionFactor: conversionFactor,
            referenceType: referenceType,
            referenceID: referenceID,
            referenceLineID: referenceLineID,
            referenceNo: referenceNo,
            remarks: remarks,
            mrp: null,
            unitID: unitID,
            packTypeID: packTypeID,
            tenantId: tenantId,
            createdBy: createdBy
        );
    }
}
