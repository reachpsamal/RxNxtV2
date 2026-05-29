// =============================================
// PharmaCare POS - Sales Management JavaScript
// =============================================

// ============ STATE ============
let selectedCustomer = null;
let saleItems = [];
let selectedPaymentMethod = 'Split';
let currentBatchInfo = null;
let editingSaleId = null;
let isPrefillingEditSale = false;
let isReturnMode = false;

let originalSaleItemsByKey = null;
let allowedReturnItemKeys = null;
let originalSaleItemsByPidBnKey = null;
let originalSaleAdditionalDiscount = 0;

const productUomOptionsCache = new Map();

function normalizeUomName(name) {
    return (name || '').toString().trim();
}

function makeSaleItemKeyFromFields(productId, batchNumber, expiryDate) {
    const pid = String(productId || '').trim().toLowerCase();
    const bn = String(batchNumber || '').trim().toLowerCase();
    const exp = expiryDate ? new Date(expiryDate) : null;
    const expKey = exp && !Number.isNaN(exp.getTime()) ? exp.toISOString().slice(0, 10) : '';
    return `${pid}|${bn}|${expKey}`;
}

function toBaseQty(qty, saleUnit, baseUnit, otherUnit, factor) {
    const q = parseFloat(qty);
    if (!Number.isFinite(q)) return 0;
    const su = normalizeUomName(saleUnit);
    const bu = normalizeUomName(baseUnit);
    const ou = normalizeUomName(otherUnit);
    const f = parseFloat(factor) || 1;
    if (!su || !bu) return q;
    if (su.toLowerCase() === bu.toLowerCase()) return q;
    if (ou && su.toLowerCase() === ou.toLowerCase() && f > 0) {
        const baseIsPcs = bu.toLowerCase() === 'pcs';
        const otherIsPcs = ou.toLowerCase() === 'pcs';
        const mappingReversed = baseIsPcs && !otherIsPcs;
        return mappingReversed ? (q * f) : (q / f);
    }
    return q;
}

function fromBaseQty(baseQty, saleUnit, baseUnit, otherUnit, factor) {
    const q = parseFloat(baseQty);
    if (!Number.isFinite(q)) return 0;
    const su = normalizeUomName(saleUnit);
    const bu = normalizeUomName(baseUnit);
    const ou = normalizeUomName(otherUnit);
    const f = parseFloat(factor) || 1;
    if (!su || !bu) return q;
    if (su.toLowerCase() === bu.toLowerCase()) return q;
    if (ou && su.toLowerCase() === ou.toLowerCase() && f > 0) {
        const baseIsPcs = bu.toLowerCase() === 'pcs';
        const otherIsPcs = ou.toLowerCase() === 'pcs';
        const mappingReversed = baseIsPcs && !otherIsPcs;
        return mappingReversed ? (q / f) : (q * f);
    }
    return q;
}

function getReturnRefundAmount() {
    if (!isReturnMode) return 0;
    return getGrandTotal();
}

function getCachedUomOptions(productId) {
    if (!productId) return null;
    return productUomOptionsCache.get(String(productId).toLowerCase()) || null;
}

function fetchUomOptions(productId) {
    if (!productId) return Promise.resolve(null);
    const key = String(productId).toLowerCase();
    const cached = productUomOptionsCache.get(key);
    if (cached && cached.__loaded) return Promise.resolve(cached);
    if (cached && cached.__loading) return cached.__loading;

    const p = fetch(`${MVC_BASE}/GetProductUomOptions?productId=${encodeURIComponent(productId)}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(r => r.ok ? r.json() : null)
        .then(data => {
            if (!data || !data.ok) {
                const fallback = { __loaded: true, baseUomName: '', otherUomName: '', conversionFactor: 1 };
                productUomOptionsCache.set(key, fallback);
                return fallback;
            }
            const opt = {
                __loaded: true,
                baseUomName: normalizeUomName(data.baseUomName),
                otherUomName: normalizeUomName(data.otherUomName),
                conversionFactor: parseFloat(data.conversionFactor) || 1
            };
            productUomOptionsCache.set(key, opt);
            return opt;
        })
        .catch(() => {
            const fallback = { __loaded: true, baseUomName: '', otherUomName: '', conversionFactor: 1 };
            productUomOptionsCache.set(key, fallback);
            return fallback;
        });

    productUomOptionsCache.set(key, { __loading: p });
    return p;
}

function updateStockItemUom(index, value) {
    const item = saleItems[index];
    if (!item || !item.productId) return;
    const prevSaleUnit = normalizeUomName(item.saleUomName) || normalizeUomName(item.uomName);
    const v = normalizeUomName(value);
    item.saleUomName = v;
    item.unitType = v;
    item.uomName = item.uomName || v;

    const uomOpt = getCachedUomOptions(item.productId);
    let baseUnit = normalizeUomName(uomOpt?.baseUomName) || normalizeUomName(item.uomName);
    let otherUnit = normalizeUomName(uomOpt?.otherUomName);
    if (baseUnit && otherUnit && baseUnit.toLowerCase() === 'pcs' && otherUnit.toLowerCase() !== 'pcs') {
        const tmp = baseUnit;
        baseUnit = otherUnit;
        otherUnit = tmp;
    }
    const factor = parseFloat(uomOpt?.conversionFactor) || 1;
    const currentPrice = parseFloat(item.price) || 0;
    const basePrice = parseFloat(item.basePrice);
    const hasBasePrice = Number.isFinite(basePrice) && basePrice > 0;
    const canConvert = otherUnit && baseUnit && factor > 0;

    // If basePrice isn't canonicalized yet, reconstruct it from the price in the *previous* unit.
    if (!hasBasePrice && canConvert) {
        if (prevSaleUnit && prevSaleUnit.toLowerCase() === otherUnit.toLowerCase()) {
            item.basePrice = parseFloat((currentPrice * factor).toFixed(2));
        } else {
            item.basePrice = currentPrice;
        }
        item.__basePriceCanonicalized = true;
    } else if (hasBasePrice && item.__basePriceCanonicalized !== true) {
        // Preserve existing canonical basePrice if it already exists.
        item.__basePriceCanonicalized = true;
    }

    const resolvedBasePrice = parseFloat(item.basePrice) || 0;

    if (otherUnit && baseUnit && v.toLowerCase() === otherUnit.toLowerCase() && factor > 0) {
        item.price = parseFloat((resolvedBasePrice / factor).toFixed(2));
    } else {
        item.price = resolvedBasePrice;
    }

    recalculateItem(index);
    recalculateBill();
    renderSaleItems();
    updateItemsCount();
}
let selectedUnitType = 'PCS';
let debounceTimer = null;
let suppressBatchAutoSelectUntil = 0;
let isSyncingSplit = false;
let lastBatchQuickAdd = { key: null, at: 0 };
let inFlightBatchAdds = new Map();

let medicineSuggestions = [];
let activeMedicineIndex = -1;

let batchSuggestions = [];
let activeBatchIndex = -1;

const MVC_BASE = '/Sales';
const API_BASE = '/api/api';

const PREFETCH_STOCKS = Array.isArray(window.__prefetchStocks) ? window.__prefetchStocks : [];

function safeNotify(msg) {
    try {
        if (typeof showToast === 'function') {
            showToast(msg, 'error');
            return;
        }
    } catch { }
    try { console.error(msg); } catch { }
}

window.addEventListener('error', function (e) {
    const message = e?.error?.message || e?.message || 'Unknown JS error';
    safeNotify(message);
});

window.addEventListener('unhandledrejection', function (e) {
    const message = e?.reason?.message || String(e?.reason || 'Unhandled promise rejection');
    safeNotify(message);
});

// ============ UTILITIES ============
function formatCurrency(amount) {
    return '₹ ' + parseFloat(amount || 0).toFixed(2);
}

function round2(amount) {
    const n = parseFloat(amount);
    if (!Number.isFinite(n)) return 0;
    return Math.round((n + Number.EPSILON) * 100) / 100;
}

function readStockValue(obj, ...keys) {
    for (const k of keys) {
        if (obj && obj[k] !== undefined && obj[k] !== null) return obj[k];
    }
    return undefined;
}

function readStockQty(stock) {
    const qty = readStockValue(stock, 'availableQty', 'AvailableQty', 'availableQuantity', 'AvailableQuantity');
    const n = parseFloat(qty);
    return Number.isFinite(n) ? n : 0;
}

function readStockUom(stock) {
    return readStockValue(stock, 'uomName', 'UomName') || 'PCS';
}

function readStockProductName(stock) {
    return readStockValue(stock, 'productName', 'ProductName') || '';
}

function readStockBatchNumber(stock) {
    return readStockValue(stock, 'batchNumber', 'BatchNumber') || '';
}

function readStockProductId(stock) {
    return readStockValue(stock, 'productId', 'ProductId');
}

function findStockByProductId(productId) {
    const pid = String(productId || '').trim().toLowerCase();
    if (!pid) return null;
    return PREFETCH_STOCKS.find(s => String(s?.productId || '').trim().toLowerCase() === pid) || null;
}

function readStockMrp(stock) {
    const v = readStockValue(stock, 'mrp', 'Mrp', 'unitPrice', 'UnitPrice');
    const n = parseFloat(v);
    return Number.isFinite(n) ? n : 0;
}

function readStockTaxName(stock) {
    return readStockValue(stock, 'taxName', 'TaxName') || '';
}

function detectGstPercentFromTaxName(taxName) {
    const s = String(taxName || '');
    const matches = s.match(/(\d+(?:\.\d+)?)\s*%?/g);
    if (!matches) return 0;

    for (const m of matches) {
        const n = parseFloat(m);
        if (n === 5 || n === 12 || n === 18) return n;
    }
    return 0;
}

function normalizeStockPayload(b) {
    // MVC JSON uses PascalCase by default; normalize to the camelCase fields used throughout JS.
    if (!b) return null;

    const productId = readStockProductId(b);
    const batchNumber = readStockBatchNumber(b);
    const productName = readStockProductName(b);
    const uomName = readStockUom(b);
    const availableQty = readStockQty(b);
    const mrp = readStockMrp(b);
    const expiryDate = readStockValue(b, 'expiryDate', 'ExpiryDate') || null;
    const taxName = readStockTaxName(b);
    const taxPercent = detectGstPercentFromTaxName(taxName);

    return {
        ...b,
        productId,
        batchNumber,
        productName,
        uomName,
        availableQty,
        mrp,
        expiryDate,
        taxName,
        taxPercent
    };
}

function formatExpiryDate(expiryDate) {
    if (!expiryDate) return '';
    const d = new Date(expiryDate);
    if (Number.isNaN(d.getTime())) return '';
    return d.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' });
}

function showToast(message, type = 'info') {
    const container = document.getElementById('toastContainer');
    const icons = { success: 'bi-check-circle-fill', error: 'bi-x-circle-fill', warning: 'bi-exclamation-triangle-fill', info: 'bi-info-circle-fill' };
    const toast = document.createElement('div');
    toast.className = `toast-item ${type}`;
    toast.innerHTML = `<i class="bi ${icons[type]}"></i> ${message}`;
    container.appendChild(toast);
    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transform = 'translateX(30px)';
        toast.style.transition = 'all 0.3s ease';
        setTimeout(() => toast.remove(), 300);
    }, 3500);
}

function debounce(func, delay = 300) {
    return function (...args) {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => func.apply(this, args), delay);
    };
}

// ============ CUSTOMER ============
$('#customerSearch').on('input', debounce(function () {
    const q = $(this).val().trim();
    const clearBtn = $('#customerSearchClear');
    clearBtn.toggleClass('show', q.length > 0);
    if (q.length < 2) {
        $('#customerDropdown').removeClass('show').empty();
        return;
    }
    fetch(`${MVC_BASE}/SearchCustomer?q=${encodeURIComponent(q)}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(r => r.text())
        .then(html => {
            const dropdown = $('#customerDropdown');
            dropdown.html(html);
            dropdown.addClass('show');
        })
        .catch(() => {
            $('#customerDropdown').removeClass('show').empty();
        });
}));

function selectCustomer(id) {
    fetch(`${MVC_BASE}/GetCustomerCard?id=${encodeURIComponent(id)}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(r => {
            if (!r.ok) throw new Error('failed');
            return r.text();
        })
        .then(html => {
            $('#selectedCustomerCard').html(html).show();
            const json = $('#selectedCustomerCard .customer-card').attr('data-customer-json');
            if (json) {
                try {
                    const obj = JSON.parse(json);
                    selectedCustomer = obj ? {
                        id: obj.id ?? obj.Id ?? null,
                        name: obj.name ?? obj.Name ?? '',
                        phone: obj.phone ?? obj.Phone ?? '',
                        email: obj.email ?? obj.Email ?? null,
                        customerCode: obj.customerCode ?? obj.CustomerCode ?? null
                    } : null;
                } catch { selectedCustomer = null; }
            }
            $('#customerDropdown').removeClass('show').empty();
            $('#customerSearch').val('').hide();
            $('#customerSearchIcon').hide();
            $('#toggleNewCustomerForm').hide();
            $('#skipCustomerBtn').hide();
            showToast(`Customer "${selectedCustomer?.name || ''}" selected`, 'success');
            updateCompleteSaleBtn();
        })
        .catch(() => {
            showToast('Failed to select customer', 'error');
        });
}

function removeCustomer() {
    if (isReturnMode) {
        showToast('Return mode: customer cannot be changed', 'warning');
        return;
    }
    selectedCustomer = null;
    $('#selectedCustomerCard').hide().empty();
    $('#customerSearch').val('').show();
    $('#customerSearchIcon').show();
    $('#toggleNewCustomerForm').show();
    $('#skipCustomerBtn').show();
    updateCompleteSaleBtn();
}

function clearCustomerSearch() {
    $('#customerSearch').val('');
    $('#customerDropdown').removeClass('show').empty();
    $('#customerSearchClear').removeClass('show');
}

function skipCustomer() {
    selectedCustomer = null;
    $('#selectedCustomerCard').html(`
        <div class="customer-card" style="position: relative; background: var(--gray-50); border-color: var(--gray-200);">
            <button class="customer-remove-top" onclick="removeCustomer()" title="Change">
                <i class="bi bi-pencil"></i>
            </button>
            <div class="customer-avatar" style="background: var(--gray-400);">
                <i class="bi bi-person" style="font-weight: normal;"></i>
            </div>
            <div class="customer-info">
                <div class="customer-name">Walk-in Customer</div>
                <div class="customer-phone">No loyalty tracking</div>
            </div>
        </div>
    `).show();
    $('#customerSearch').hide();
    $('#customerSearchIcon').hide();
    $('#toggleNewCustomerForm').hide();
    $('#skipCustomerBtn').hide();
    updateCompleteSaleBtn();
}

function toggleNewCustomerForm() {
    $('#newCustomerForm').toggleClass('show');
}

let lastNewCustomerPrefill = { field: null, value: null };

function openNewCustomerFromSearch() {
    const q = ($('#customerSearch').val() || '').trim();
    const form = $('#newCustomerForm');
    form.addClass('show');

    const nameEl = $('#newCustName');
    const phoneEl = $('#newCustPhone');

    if (q.length < 2) {
        // If user cleared the search, start fresh (do not keep previous prefill)
        nameEl.val('');
        phoneEl.val('');
        lastNewCustomerPrefill = { field: null, value: null };
        nameEl.focus();
        return;
    }

    const normalized = q.replace(/[\s\-\+\(\)]/g, '');
    const isPhone = /^\d{4,}$/.test(normalized);

    if (isPhone) {
        const canOverwritePhone = !phoneEl.val() || (lastNewCustomerPrefill.field === 'phone' && phoneEl.val() === lastNewCustomerPrefill.value);
        if (canOverwritePhone) {
            phoneEl.val(normalized);
            lastNewCustomerPrefill = { field: 'phone', value: normalized };
        }
        if (!nameEl.val()) {
            nameEl.focus();
        } else {
            phoneEl.focus();
        }
        return;
    }

    const canOverwriteName = !nameEl.val() || (lastNewCustomerPrefill.field === 'name' && nameEl.val() === lastNewCustomerPrefill.value);
    if (canOverwriteName) {
        nameEl.val(q);
        lastNewCustomerPrefill = { field: 'name', value: q };
    }
    nameEl.focus();
}

function saveNewCustomer() {
    const name = $('#newCustName').val().trim();
    const phone = $('#newCustPhone').val().trim();
    if (!name) { showToast('Customer name is required', 'error'); return; }
    if (!phone) { showToast('Phone number is required', 'error'); return; }

    const token = document.querySelector('#completeSaleForm input[name="__RequestVerificationToken"]')?.value;
    const form = new URLSearchParams();
    form.set('name', name);
    form.set('phone', phone);
    if (token) form.set('__RequestVerificationToken', token);

    fetch(`${MVC_BASE}/CreateCustomer`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8', 'X-Requested-With': 'XMLHttpRequest' },
        body: form.toString()
    })
        .then(async r => {
            const txt = await r.text();
            if (!r.ok) throw new Error(txt || 'Failed');
            return txt;
        })
        .then(html => {
            toggleNewCustomerForm(false);
            $('#selectedCustomerCard').html(html).show();
            const json = $('#selectedCustomerCard .customer-card').attr('data-customer-json');
            if (json) {
                try {
                    const obj = JSON.parse(json);
                    selectedCustomer = obj ? {
                        id: obj.id ?? obj.Id ?? null,
                        name: obj.name ?? obj.Name ?? '',
                        phone: obj.phone ?? obj.Phone ?? '',
                        email: obj.email ?? obj.Email ?? null
                    } : null;
                } catch { selectedCustomer = null; }
            }
            $('#customerSearch').val('').hide();
            $('#toggleNewCustomerForm').hide();
            $('#skipCustomerBtn').hide();
            $('#newCustName').val('');
            $('#newCustPhone').val('');
            showToast('Customer saved successfully', 'success');
            updateCompleteSaleBtn();
        })
        .catch(err => {
            showToast(err?.message || 'Failed to save customer', 'error');
        });
}

// ============ MEDICINE TABS ============
function switchMedicineTab(tab) {
    $('.pharmacy-tab').removeClass('active');
    $('.tab-content-pharmacy').removeClass('active');
    if (tab === 'batch') {
        $('#tabBatch').addClass('active');
        $('#tabContentBatch').addClass('active');
    } else if (tab === 'direct') {
        $('#tabDirect').addClass('active');
        $('#tabContentDirect').addClass('active');
    } else {
        $('#tabAdvanced').addClass('active');
        $('#tabContentAdvanced').addClass('active');
    }
}

function addFromDirectSelection(productId, batchNumber) {
    addFromAdvancedSearch(productId, batchNumber);
    $('#medicineDropdown').removeClass('show').empty();
    $('#medicineBatchesCard').removeClass('show').empty();
    $('#medicineSearch').val('');
}

// ============ BATCH SEARCH (TAB A) ============
function fetchBatchSearchResults(q, onDone) {
    const query = (q || '').trim();
    if (query.length < 2) {
        $('#batchDropdown').removeClass('show').empty();
        batchSuggestions = [];
        activeBatchIndex = -1;
        onDone([]);
        return;
    }

    const results = PREFETCH_STOCKS
        .filter(s => (s?.batchNumber || '').toString().toLowerCase().includes(query.toLowerCase()))
        .sort((a, b) => String(a?.batchNumber || '').localeCompare(String(b?.batchNumber || ''), 'en', { sensitivity: 'base' }))
        .slice(0, 20);

    batchSuggestions = results;
    activeBatchIndex = results.length ? 0 : -1;

    renderBatchDropdown(results);
    onDone(results.map(r => ({ productId: r.productId, batchNumber: r.batchNumber })));
}

$('#batchSearch').on('input', debounce(function () {
    const q = $(this).val().trim();
    fetchBatchSearchResults(q, function () { });
}));

function renderBatchDropdown(data) {
    const dropdown = $('#batchDropdown');
    const items = Array.isArray(data) ? data : [];
    if (!items.length) {
        dropdown
            .html('<div class="autocomplete-no-results"><i class="bi bi-box"></i> No batch found</div>')
            .addClass('show');
        return;
    }

    const html = items.map((b, idx) => {
        const isExpired = !!b.isExpired;
        const isNearExpiry = !!b.isNearExpiry;
        const expiryClass = isExpired ? 'badge-expired' : (isNearExpiry ? 'badge-expiry-warning' : 'badge-stock');
        const expiryText = isExpired ? 'EXPIRED' : (isNearExpiry ? 'Near Expiry' : 'Valid');
        const exp = b.expiryDate ? new Date(b.expiryDate) : null;
        const expTxt = exp && !Number.isNaN(exp.getTime())
            ? exp.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })
            : '-';

        const activeClass = idx === activeBatchIndex ? 'active' : '';

        return `
            <div class="autocomplete-item ${activeClass}" data-index="${idx}" data-product-id="${b.productId}" data-batch-number="${String(b.batchNumber || '')}" onclick="addFromBatchSelection('${b.productId}', '${String(b.batchNumber || '').replace(/'/g, "\\'")}')">
                <div class="item-name">${String(b.productName || '')}</div>
                <div class="item-detail">
                    Batch: <strong>${String(b.batchNumber || '')}</strong> &middot;
                    Exp: ${expTxt}
                    <span class="item-badge ${expiryClass}">${expiryText}</span> &middot;
                    Stock: ${formatNumber(b.availableQty)} ${String(b.uomName || '')}
                </div>
            </div>
        `;
    }).join('');

    dropdown.html(html).addClass('show');
}

function formatNumber(n) {
    const v = parseFloat(n);
    return Number.isFinite(v) ? (Number.isInteger(v) ? String(v) : String(v)) : '0';
}

function findPrefetchedStock(productId, batchNumber) {
    const pid = String(productId || '').trim().toLowerCase();
    const bn = String(batchNumber || '').trim().toLowerCase();
    if (!pid || !bn) return null;
    return PREFETCH_STOCKS.find(s => String(s?.productId || '').trim().toLowerCase() === pid && String(s?.batchNumber || '').trim().toLowerCase() === bn) || null;
}

function findStockByProductBatch(productId, batchNumber) {
    return findPrefetchedStock(productId, batchNumber);
}

function addFromBatchSelection(productId, batchNumber) {
    // Make batch selection behave like direct medicine selection: quick-add 1 qty.
    suppressBatchAutoSelectUntil = Date.now() + 800;
    $('#batchDropdown').removeClass('show').empty();
    $('#batchSearch').val('');
    addFromAdvancedSearch(productId, batchNumber);
}

// ============ MEDICINE SEARCH (TAB B) ============
function renderMedicineDropdown(items) {
    const dropdown = $('#medicineDropdown');
    const results = Array.isArray(items) ? items : [];
    if (!results.length) {
        dropdown
            .html('<div class="autocomplete-no-results"><i class="bi bi-capsule"></i> No medicine found</div>')
            .addClass('show');
        return;
    }

    const html = results.map((m, idx) => {
        const manufacturer = (m.manufacturer || '').trim();
        const manufacturerPrefix = manufacturer ? `${manufacturer} · ` : '';
        const activeClass = idx === activeMedicineIndex ? 'active' : '';

        return `
            <div class="autocomplete-item ${activeClass}" data-index="${idx}" onclick="addFromDirectSelection('${m.productId}', '${String(m.batchNumber || '').replace(/'/g, "\\'")}')">
                <div class="item-name">${String(m.productName || '')}</div>
                <div class="item-detail">
                    ${manufacturerPrefix}Batch: <strong>${String(m.batchNumber || '')}</strong> &middot; Stock: ${formatNumber(m.availableQty)} ${String(m.uomName || '')}
                </div>
            </div>
        `;
    }).join('');

    dropdown.html(html).addClass('show');
}

$('#medicineSearch').on('input', debounce(function () {
    const q = $(this).val().trim();
    if (q.length < 2) {
        $('#medicineDropdown').removeClass('show').empty();
        medicineSuggestions = [];
        activeMedicineIndex = -1;
        return;
    }
    const query = q.toLowerCase();
    const results = PREFETCH_STOCKS
        .filter(s => (s?.productName || '').toString().toLowerCase().includes(query))
        .sort((a, b) => {
            const byName = String(a?.productName || '').localeCompare(String(b?.productName || ''), 'en', { sensitivity: 'base' });
            if (byName !== 0) return byName;
            return String(a?.batchNumber || '').localeCompare(String(b?.batchNumber || ''), 'en', { sensitivity: 'base' });
        })
        .slice(0, 20);

    medicineSuggestions = results;
    activeMedicineIndex = results.length ? 0 : -1;
    renderMedicineDropdown(results);
}));

$('#medicineSearch').on('keydown', function (e) {
    const dropdown = $('#medicineDropdown');
    if (!dropdown.hasClass('show') || !Array.isArray(medicineSuggestions) || !medicineSuggestions.length) return;

    if (e.key === 'ArrowDown') {
        e.preventDefault();
        activeMedicineIndex = activeMedicineIndex < 0 ? 0 : (activeMedicineIndex + 1) % medicineSuggestions.length;
        renderMedicineDropdown(medicineSuggestions);
        return;
    }

    if (e.key === 'ArrowUp') {
        e.preventDefault();
        activeMedicineIndex = activeMedicineIndex < 0
            ? (medicineSuggestions.length - 1)
            : (activeMedicineIndex - 1 + medicineSuggestions.length) % medicineSuggestions.length;
        renderMedicineDropdown(medicineSuggestions);
        return;
    }

    if (e.key === 'Enter') {
        if (activeMedicineIndex < 0 || activeMedicineIndex >= medicineSuggestions.length) return;
        e.preventDefault();
        const m = medicineSuggestions[activeMedicineIndex];
        if (!m) return;
        addFromDirectSelection(m.productId, m.batchNumber);
        return;
    }

    if (e.key === 'Escape') {
        e.preventDefault();
        dropdown.removeClass('show').empty();
        return;
    }
});

$('#batchSearch').on('keydown', function (e) {
    const dropdown = $('#batchDropdown');
    if (!dropdown.hasClass('show') || !Array.isArray(batchSuggestions) || !batchSuggestions.length) return;

    if (e.key === 'ArrowDown') {
        e.preventDefault();
        activeBatchIndex = activeBatchIndex < 0 ? 0 : (activeBatchIndex + 1) % batchSuggestions.length;
        renderBatchDropdown(batchSuggestions);
        return;
    }

    if (e.key === 'ArrowUp') {
        e.preventDefault();
        activeBatchIndex = activeBatchIndex < 0
            ? (batchSuggestions.length - 1)
            : (activeBatchIndex - 1 + batchSuggestions.length) % batchSuggestions.length;
        renderBatchDropdown(batchSuggestions);
        return;
    }

    if (e.key === 'Enter') {
        if (activeBatchIndex < 0 || activeBatchIndex >= batchSuggestions.length) return;
        e.preventDefault();
        const b = batchSuggestions[activeBatchIndex];
        if (!b) return;
        addFromBatchSelection(b.productId, b.batchNumber);
        return;
    }

    if (e.key === 'Escape') {
        e.preventDefault();
        dropdown.removeClass('show').empty();
        return;
    }
});

// ============ CART MANAGEMENT ============
function addToCart() {
    if (!currentBatchInfo) { showToast('Please select a batch first', 'warning'); return; }

    if (isReturnMode) {
        const key = `${String(currentBatchInfo.productId || '').trim().toLowerCase()}|${String(currentBatchInfo.batchNumber || '').trim().toLowerCase()}`;
        if (!allowedReturnItemKeys || !allowedReturnItemKeys.has(key)) {
            showToast('Only items from the selected sale invoice can be returned', 'error');
            return;
        }
    }

    const qty = parseInt($('#addQty').val()) || 0;
    const unitType = selectedUnitType;

    if (qty <= 0) { showToast('Please enter a valid quantity', 'error'); return; }

    const maxQty = readStockQty(currentBatchInfo);
    const uomOpt = getCachedUomOptions(currentBatchInfo.productId);
    const baseUnit = normalizeUomName(readStockUom(currentBatchInfo)) || normalizeUomName(uomOpt?.baseUomName);
    const otherUnit = normalizeUomName(uomOpt?.otherUomName);
    const factor = parseFloat(uomOpt?.conversionFactor) || 1;
    const effectiveMaxQty = (otherUnit && baseUnit && normalizeUomName(selectedUnitType).toLowerCase() === otherUnit.toLowerCase() && factor > 0)
        ? (maxQty * factor)
        : maxQty;
    if (qty > effectiveMaxQty) { showToast(`Only ${effectiveMaxQty} ${unitType.toLowerCase()}s available`, 'error'); return; }

    // Check if already in cart
    const existingIndex = saleItems.findIndex(i => i.productId === currentBatchInfo.productId && i.batchNumber === currentBatchInfo.batchNumber && i.uomName === readStockUom(currentBatchInfo));

    if (existingIndex >= 0) {
        saleItems[existingIndex].quantity = qty;
        saleItems[existingIndex].unitType = unitType;
        if (!saleItems[existingIndex].saleUomName) {
            saleItems[existingIndex].saleUomName = saleItems[existingIndex].uomName || readStockUom(currentBatchInfo);
        }
        saleItems[existingIndex].basePrice = readStockMrp(currentBatchInfo);
        saleItems[existingIndex].price = saleItems[existingIndex].basePrice;
        saleItems[existingIndex].availableQty = maxQty;
        // keep existing slab unless the item didn't have it before
        if (saleItems[existingIndex].taxPercent === undefined || saleItems[existingIndex].taxPercent === null) {
            saleItems[existingIndex].taxPercent = currentBatchInfo.taxPercent ?? 0;
        }
        recalculateItem(existingIndex);
        showToast(`Updated ${currentBatchInfo.productName} quantity to ${qty} ${unitType.toLowerCase()}s`, 'info');
    } else {
        const price = readStockMrp(currentBatchInfo);
        const taxPercent = parseFloat(currentBatchInfo.taxPercent) || 0;
        saleItems.push({
            productId: currentBatchInfo.productId,
            productName: currentBatchInfo.productName,
            batchNumber: currentBatchInfo.batchNumber,
            expiryDate: currentBatchInfo.expiryDate,
            uomName: readStockUom(currentBatchInfo),
            saleUomName: readStockUom(currentBatchInfo),
            quantity: qty,
            unitType: unitType,
            basePrice: price,
            price: price,
            discountPercent: 0,
            discountAmount: 0,
            taxPercent: taxPercent,
            taxAmount: 0,
            total: 0,
            availableQty: maxQty
        });
        recalculateItem(saleItems.length - 1);
        showToast(`Added ${currentBatchInfo.productName} x${qty} ${unitType.toLowerCase()}s`, 'success');
    }

    renderSaleItems();
    // Cart changed => additional discount base changed; keep amount in sync before bill calc
    syncAdditionalDiscountAmountFromPercent();
    recalculateBill();
    updateItemsCount();
    updateCompleteSaleBtn();

    // Clear batch selection
    currentBatchInfo = null;
    selectedUnitType = 'PCS';
    $('#batchInfoCard, #medicineBatchesCard').removeClass('show').empty();
    $('#batchSearch, #medicineSearch').val('');
}

async function addFromAdvancedSearch(productId, batchNumber) {
    const addKey = `${String(productId || '').trim().toLowerCase()}|${String(batchNumber || '').trim().toLowerCase()}`;

    if (isReturnMode) {
        if (!allowedReturnItemKeys || !allowedReturnItemKeys.has(addKey)) {
            showToast('Only items from the selected sale invoice can be returned', 'error');
            return;
        }
    }

    const now = Date.now();
    const lastInFlightAt = inFlightBatchAdds.get(addKey);
    if (lastInFlightAt && (now - lastInFlightAt) < 1200) {
        return;
    }
    inFlightBatchAdds.set(addKey, now);

    try {
        // Real-time stock check from server before adding
        var stockCheck;
        try {
            stockCheck = await $.get(`${API_BASE}/stocks/by-product-batch?productId=${encodeURIComponent(productId)}&batchNumber=${encodeURIComponent(batchNumber)}`);
        } catch (e) {
            stockCheck = null;
        }

        var liveAvailableQty;
        var stockSourceName;
        if (stockCheck) {
            liveAvailableQty = readStockQty(stockCheck);
            stockSourceName = stockCheck.productName || 'Unknown';
        } else {
            // Fall back to prefetched data if API fails
            const prefetched = findPrefetchedStock(productId, batchNumber);
            liveAvailableQty = prefetched ? readStockQty(prefetched) : 0;
            stockSourceName = prefetched ? readStockProductName(prefetched) : 'Unknown';
        }

        if (liveAvailableQty <= 0) {
            showStockIssueModal([{
                productName: stockSourceName,
                batchNumber: batchNumber,
                message: `"${stockSourceName}" (${batchNumber}) is out of stock`,
                availableQty: 0,
                requiredQty: 1
            }]);
            return;
        }

        const match = findPrefetchedStock(productId, batchNumber);
        if (!match) {
            showToast('Stock not found in preloaded list', 'error');
            return;
        }

        const b = normalizeStockPayload(match);
        if (!b) {
            showToast('Failed to add item', 'error');
            return;
        }

        // Use live stock qty for accuracy
        b.availableQty = liveAvailableQty;
        currentBatchInfo = b;

        const fixedUnit = readStockUom(b);
        selectedUnitType = fixedUnit;

        const availableQty = liveAvailableQty;
        const productName = readStockProductName(b);
        const batchNo = readStockBatchNumber(b);

        currentBatchInfo.availableQty = availableQty;
        currentBatchInfo.uomName = fixedUnit;
        currentBatchInfo.productName = productName;
        currentBatchInfo.batchNumber = batchNo;

        const existingIndex = saleItems.findIndex(i => i.productId === currentBatchInfo.productId && i.batchNumber === currentBatchInfo.batchNumber && i.uomName === readStockUom(currentBatchInfo));
        if (existingIndex >= 0) {
            const newQty = Math.min((saleItems[existingIndex].quantity || 0) + 1, availableQty);
            saleItems[existingIndex].quantity = newQty;
            saleItems[existingIndex].unitType = fixedUnit;
            if (!saleItems[existingIndex].saleUomName) {
                saleItems[existingIndex].saleUomName = saleItems[existingIndex].uomName || fixedUnit;
            }
            saleItems[existingIndex].basePrice = readStockMrp(currentBatchInfo);
            saleItems[existingIndex].price = saleItems[existingIndex].basePrice;
            saleItems[existingIndex].availableQty = availableQty;
            if (saleItems[existingIndex].taxPercent === undefined || saleItems[existingIndex].taxPercent === null) {
                saleItems[existingIndex].taxPercent = currentBatchInfo.taxPercent ?? 0;
            }
            recalculateItem(existingIndex);
            showToast(`Increased ${productName} quantity`, 'info');
        } else {
            const price = readStockMrp(currentBatchInfo);
            const taxPercent = parseFloat(currentBatchInfo.taxPercent) || 0;
            saleItems.push({
                productId: currentBatchInfo.productId,
                productName: productName,
                batchNumber: batchNo,
                expiryDate: currentBatchInfo.expiryDate,
                uomName: fixedUnit,
                saleUomName: fixedUnit,
                quantity: 1,
                unitType: fixedUnit,
                basePrice: price,
                price: price,
                discountPercent: 0,
                discountAmount: 0,
                taxPercent: taxPercent,
                taxAmount: 0,
                total: 0,
                availableQty: availableQty
            });
            recalculateItem(saleItems.length - 1);
            showToast(`Added ${productName}`, 'success');
        }

        currentBatchInfo = null;
    recalculateBill();
    renderSaleItems();
    syncAdditionalDiscountAmountFromPercent();
    updateItemsCount();
    updateCompleteSaleBtn();
    } finally {
        // Allow re-add after local operation finishes; quick-add is still protected by timestamp guard
        inFlightBatchAdds.delete(addKey);
    }
}

function recalculateItem(index) {
    const item = saleItems[index];
    const mrp = parseFloat(item.mrp || item.price) || 0;
    const qty = parseFloat(item.quantity) || 0;
    const discPercent = parseFloat(item.discountPercent) || 0;

    const gross = mrp * qty;
    item.discountAmount = round2(gross * discPercent / 100);
    item.itemNetAmount = round2(Math.max(0, gross - item.discountAmount));
}

/**
 * GST BILLING ENGINE
 * Steps:
 *  1. MRP x Qty = Item Gross
 *  2. Item Discount on Gross
 *  3. Item Net = Gross - Discount
 *  4. Bill Gross = sum(Item Nets)
 *  5. Bill Discount = Bill Gross x billDiscountPercent / 100
 *  6. Distribute Bill Discount Proportionally by contribution ratio
 *  7. After allocation -> Final Inclusive = Item Net - allocation -> Taxable + GST
 *  8-9. Round + Final Totals
 */
function calculateInvoice(items, billDiscountPercent) {
    function r2(v) { return parseFloat((v || 0).toFixed(2)); }

    items.forEach(item => {
        item.qty = item.qty || 1;
        item.gstPercent = item.gstPercent || 0;

        item.itemGrossAmount = r2(item.mrp * item.qty);

        var discAmt = 0;
        if (item.itemDiscountType === 'PERCENT') {
            discAmt = item.itemGrossAmount * (item.itemDiscountValue / 100);
        } else if (item.itemDiscountType === 'AMOUNT') {
            discAmt = item.itemDiscountValue || 0;
        }
        item.itemDiscountAmount = r2(discAmt);

        item.itemNetAmount = r2(item.itemGrossAmount - item.itemDiscountAmount);
    });

    var billGrossAmount = r2(items.reduce(function (s, x) { return s + x.itemNetAmount; }, 0));

    var billDiscountAmount = r2(billGrossAmount * (billDiscountPercent / 100));

    items.forEach(function (item) {
        var ratio = billGrossAmount > 0 ? item.itemNetAmount / billGrossAmount : 0;
        item.billDiscountAllocated = r2(billDiscountAmount * ratio);
        item.finalInclusiveAmount = r2(item.itemNetAmount - item.billDiscountAllocated);
    });

    items.forEach(function (item) {
        var gstFactor = 1 + (item.gstPercent / 100);
        item.taxableAmount = r2(item.finalInclusiveAmount / gstFactor);
        item.totalGSTAmount = r2(item.finalInclusiveAmount - item.taxableAmount);
        item.cgstAmount = r2(item.totalGSTAmount / 2);
        item.sgstAmount = r2(item.totalGSTAmount / 2);
        item.igstAmount = 0;
        item.finalAmount = item.finalInclusiveAmount;
    });

    var summary = {
        billGrossAmount: r2(billGrossAmount),
        billDiscountPercent: r2(billDiscountPercent),
        billDiscountAmount: r2(billDiscountAmount),
        taxableAmount: r2(items.reduce(function (s, x) { return s + x.taxableAmount; }, 0)),
        cgstAmount: r2(items.reduce(function (s, x) { return s + x.cgstAmount; }, 0)),
        sgstAmount: r2(items.reduce(function (s, x) { return s + x.sgstAmount; }, 0)),
        igstAmount: r2(items.reduce(function (s, x) { return s + x.igstAmount; }, 0)),
        totalGSTAmount: r2(items.reduce(function (s, x) { return s + x.totalGSTAmount; }, 0)),
        netAmount: r2(items.reduce(function (s, x) { return s + x.finalAmount; }, 0))
    };

    return { items: items, summary: summary };
}

function computeTaxBreakupBySlab() {
    const result = {
        5: { cgst: 0, sgst: 0 },
        12: { cgst: 0, sgst: 0 },
        18: { cgst: 0, sgst: 0 }
    };

    const slabTax = { 5: 0, 12: 0, 18: 0 };

    saleItems.forEach(item => {
        const gst = parseFloat(item.taxPercent);
        if (!(gst === 5 || gst === 12 || gst === 18)) return;
        const lineTax = round2(parseFloat(item.taxAmount) || 0);
        slabTax[gst] = round2(slabTax[gst] + lineTax);
    });

    [5, 12, 18].forEach(rate => {
        const half = round2(slabTax[rate] / 2);
        const otherHalf = round2(slabTax[rate] - half);
        result[rate].cgst = half;
        result[rate].sgst = otherHalf;
    });

    return result;
}

function updateTaxBreakupUI() {
    const buckets = computeTaxBreakupBySlab();

    const set = (id, value) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.textContent = formatCurrency(value);
    };

    set('cgstAmt5', buckets[5].cgst);
    set('cgstAmt12', buckets[12].cgst);
    set('cgstAmt18', buckets[18].cgst);

    set('sgstAmt5', buckets[5].sgst);
    set('sgstAmt12', buckets[12].sgst);
    set('sgstAmt18', buckets[18].sgst);
}

function formatExpiryDate(expiryDate) {
    if (!expiryDate) return '';
    const d = new Date(expiryDate);
    if (Number.isNaN(d.getTime())) return '';
    return d.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' });
}

function getMaxQtyForItem(item) {
    if (isReturnMode && originalSaleItemsByKey) {
        const key = makeSaleItemKeyFromFields(item.productId, item.batchNumber, item.expiryDate);
        const pidBnKey = `${String(item.productId || '').trim().toLowerCase()}|${String(item.batchNumber || '').trim().toLowerCase()}`;
        let old = originalSaleItemsByKey[key];
        if (!old && originalSaleItemsByPidBnKey) old = originalSaleItemsByPidBnKey[pidBnKey];
        if (old) {
            const oldQty = Number.isFinite(parseFloat(old.qty)) ? parseFloat(old.qty) : (parseFloat(old.baseQty) || 0);
            const oldUnit = old.saleUomName || old.uomName || item.saleUomName || item.uomName;

            const uomOpt = getCachedUomOptions(item.productId);
            let baseUnit = normalizeUomName(uomOpt?.baseUomName) || normalizeUomName(item.uomName);
            let otherUnit = normalizeUomName(uomOpt?.otherUomName);
            const saleUnit = normalizeUomName(item.saleUomName) || normalizeUomName(item.uomName) || baseUnit;
            const factor = parseFloat(uomOpt?.conversionFactor) || 1;

            const oldBaseQty = toBaseQty(oldQty, oldUnit, baseUnit, otherUnit, factor);
            return fromBaseQty(oldBaseQty, saleUnit, baseUnit, otherUnit, factor);
        }
    }

    const stockQty = item.availableQty ?? item.availableQty;
    if (stockQty !== undefined && stockQty !== null) {
        const n = parseFloat(stockQty);
        const baseQty = Number.isFinite(n) ? n : 0;

        if (item.productId) {
            const uomOpt = getCachedUomOptions(item.productId);
            let baseUnitName = normalizeUomName(uomOpt?.baseUomName) || normalizeUomName(item.uomName);
            let otherUnitName = normalizeUomName(uomOpt?.otherUomName);
            if (baseUnitName && otherUnitName && baseUnitName.toLowerCase() === 'pcs' && otherUnitName.toLowerCase() !== 'pcs') {
                const tmp = baseUnitName;
                baseUnitName = otherUnitName;
                otherUnitName = tmp;
            }
            const saleUnit = normalizeUomName(item.saleUomName) || baseUnitName;
            const factor = parseFloat(uomOpt?.conversionFactor) || 1;

            if (otherUnitName && baseUnitName && saleUnit.toLowerCase() === otherUnitName.toLowerCase() && factor > 0) {
                return baseQty * factor;
            }
        }

        return baseQty;
    }

    const stripQty = item.stripQuantity ?? item.availableQuantity ?? 0;
    const tps = item.tabletPerStrip || 0;
    if (item.unitType === 'Strip') return stripQty;
    return stripQty * tps;
}

function updateItemUnitType(index, value) {
    const item = saleItems[index];
    if (item.productId) {
        item.unitType = item.uomName || 'PCS';
        renderSaleItems();
        return;
    }
    const unitType = value === 'Tablet' ? 'Tablet' : 'Strip';
    item.unitType = unitType;

    if (unitType === 'Strip') item.price = item.sellingPriceStrip ?? item.price;
    else item.price = item.sellingPriceTablet ?? item.price;

    const maxQty = getMaxQtyForItem(item);
    if (item.quantity > maxQty) {
        item.quantity = Math.max(1, maxQty);
        showToast(`Quantity adjusted to available stock (${maxQty})`, 'info');
    }

    recalculateItem(index);
    recalculateBill();
    renderSaleItems();
    updateItemsCount();
}

function renderSaleItems() {
    const tbody = $('#saleItemsBody');
    tbody.empty();

    if (saleItems.length === 0) {
        $('#emptyCart').show();
        $('#saleItemsTable').hide();
        $('#clearCartBtn').hide();
        $('#saleItemsCount').hide();
        $('#itemCount').text('0 items');
        return;
    }

    $('#emptyCart').hide();
    $('#saleItemsTable').show();
    $('#clearCartBtn').show();
    $('#saleItemsCount').show();
    $('#itemCount').text(`${saleItems.length} item${saleItems.length > 1 ? 's' : ''}`);

    saleItems.forEach((item, index) => {
        const displayName = item.productName || item.medicineName || '';
        const displayUnit = item.saleUomName || item.uomName || item.unitType || 'PCS';
        const isStockItem = !!item.productId;
        const maxQty = getMaxQtyForItem(item);
        const maxQtyAttr = (maxQty && maxQty > 0) ? `max="${maxQty}"` : '';
        const uomOpt = isStockItem ? getCachedUomOptions(item.productId) : null;
        let baseUnitName = normalizeUomName(uomOpt?.baseUomName) || normalizeUomName(item.uomName) || 'PCS';
        let otherUnitName = normalizeUomName(uomOpt?.otherUomName);
        if (baseUnitName && otherUnitName && baseUnitName.toLowerCase() === 'pcs' && otherUnitName.toLowerCase() !== 'pcs') {
            const tmp = baseUnitName;
            baseUnitName = otherUnitName;
            otherUnitName = tmp;
        }
        const canSwitchStockUom = !!(uomOpt && uomOpt.__loaded && otherUnitName && otherUnitName.toLowerCase() !== baseUnitName.toLowerCase());
        const currentSaleUnit = normalizeUomName(item.saleUomName) || normalizeUomName(item.uomName) || baseUnitName;

        if (isStockItem && uomOpt && uomOpt.__loaded) {
            const factor = parseFloat(uomOpt?.conversionFactor) || 1;
            const currentPrice = parseFloat(item.price) || 0;

            if (!item.__basePriceCanonicalized && factor > 0) {
                if (otherUnitName && currentSaleUnit.toLowerCase() === otherUnitName.toLowerCase()) {
                    item.basePrice = parseFloat((currentPrice * factor).toFixed(2));
                } else {
                    item.basePrice = currentPrice;
                }
                item.__basePriceCanonicalized = true;
            }

            const basePrice = parseFloat(item.basePrice);
            if (Number.isFinite(basePrice) && basePrice > 0) {
                if (otherUnitName && currentSaleUnit.toLowerCase() === otherUnitName.toLowerCase() && factor > 0) {
                    item.price = parseFloat((basePrice / factor).toFixed(2));
                } else {
                    item.price = basePrice;
                }
            }
        }

        if (isStockItem && !uomOpt) {
            fetchUomOptions(item.productId).then(() => {
                renderSaleItems();
            });
        }
        tbody.append(`
            <tr class="row-highlight">
                <td style="color:var(--gray-400); font-weight:600;">${index + 1}</td>
                <td>
                    <div style="font-weight:600;">${displayName}</div>
                </td>
                <td>
                    <div style="font-size:0.78rem; color: var(--gray-600); font-weight:700;">${item.batchNumber}</div>
                    <div style="font-size:0.72rem; color: var(--gray-400); margin-top:2px;">Exp: ${formatExpiryDate(item.expiryDate)}</div>
                </td>
                <td>
                    ${isStockItem ? (canSwitchStockUom ? `
                    <select class="item-unit-select" onchange="updateStockItemUom(${index}, this.value)" id="unit-${index}">
                        <option value="${baseUnitName}" ${currentSaleUnit === baseUnitName ? 'selected' : ''}>${baseUnitName}</option>
                        <option value="${otherUnitName}" ${currentSaleUnit === otherUnitName ? 'selected' : ''}>${otherUnitName}</option>
                    </select>` : `<span style="font-weight:700;color:var(--gray-700);">${displayUnit}</span>`) : `
                    <select class="item-unit-select" onchange="updateItemUnitType(${index}, this.value)" id="unit-${index}">
                        <option value="Strip" ${item.unitType === 'Strip' ? 'selected' : ''}>Strip</option>
                        <option value="Tablet" ${item.unitType === 'Tablet' ? 'selected' : ''}>Tablet</option>
                    </select>`}
                </td>
                <td>
                     <input type="number" class="item-qty-input" value="${item.quantity}" ${maxQtyAttr} id="qty-${index}">

                    

                </td>
                <td style="font-variant-numeric: tabular-nums;">${formatCurrency(item.mrp || item.price)}</td>
                <td>
                    <input type="number" class="item-discount-input" value="${item.discountPercent}" min="0" max="100" step="0.5"
                            oninput="updateItemDiscountLive(${index}, this.value)" onchange="updateItemDiscount(${index}, this.value)" id="disc-${index}">
                </td>
                <td id="discAmt-${index}" style="color: var(--accent-600); font-variant-numeric: tabular-nums;">${formatCurrency(item.discountAmount)}</td>
                <td id="taxAmt-${index}" style="font-variant-numeric: tabular-nums;">${formatCurrency(item.taxAmount || 0)}</td>
                <td id="lineTotal-${index}" class="text-right" style="font-weight:700; font-variant-numeric: tabular-nums;">${formatCurrency(item.total)}</td>
                <td>
                    ${isReturnMode ? '' : `<button class="btn-remove" onclick="showDeleteConfirm(${index})" title="Remove">
                        <i class="bi bi-trash3"></i>
                    </button>`}
                </td>
            </tr>
        `);
    });
}

function updateItemsCount() {
    var show = saleItems.length > 0;
    $('#saleItemsCount').toggle(show);
    $('#itemsCountValue').text(saleItems.length);
}

function updateItemQuantity(index, value) {
    const qty = parseFloat(value);
    if (!Number.isFinite(qty)) {
        $(`#qty-${index}`).val(saleItems[index].quantity);
        return;
    }

    const minQty = isReturnMode ? 0 : 1;
    if (qty < minQty) {
        showToast(`Quantity must be at least ${minQty}`, 'error');
        $(`#qty-${index}`).val(saleItems[index].quantity);
        return;
    }
    const maxQty = getMaxQtyForItem(saleItems[index]);
    if (maxQty > 0 && qty > maxQty) {
        showToast(isReturnMode
            ? `Return mode: quantity cannot exceed original sale quantity (${maxQty})`
            : `Only ${maxQty} ${saleItems[index].unitType.toLowerCase()}(s) available`,
            'error');
        $(`#qty-${index}`).val(saleItems[index].quantity);
        return;
    }

    saleItems[index].quantity = qty;
    recalculateItem(index);
    recalculateBill();
    renderSaleItems();

    // Keep additional discount fields aligned with subtotal changes
    syncAdditionalDiscountAmountFromPercent();
    updateItemsCount();

    //saleItems[index].quantity = qty;
    //recalculateItem(index);

    //const item = saleItems[index];

    //// Update only changed fields
    //$(`#discAmt-${index}`).text(formatCurrency(item.discountAmount));
    //$(`#taxAmt-${index}`).text(formatCurrency(item.taxAmount || 0));
    //$(`#lineTotal-${index}`).text(formatCurrency(item.total));

    //recalculateBill();
    //syncAdditionalDiscountAmountFromPercent();
    //updateItemsCount();
}

function updateItemDiscount(index, value) {
    const disc = parseFloat(value);
    if (!Number.isFinite(disc) || disc < 0 || disc > 100) {
        showToast('Discount must be between 0 and 100%', 'error');
        $(`#disc-${index}`).val(saleItems[index].discountPercent);
        return;
    }
    $(`#disc-${index}`).val(disc);
    updateItemDiscountLive(index, disc);
}

function updateItemDiscountLive(index, value) {
    if (value === '' || value === null || value === undefined) {
        saleItems[index].discountPercent = 0;
    } else {
        const disc = parseFloat(value);
        if (!Number.isFinite(disc)) {
            return;
        }
        if (disc < 0 || disc > 100) {
            return;
        }
        saleItems[index].discountPercent = disc;
    }

    recalculateItem(index);
    recalculateBill();

    const item = saleItems[index];
    $(`#discAmt-${index}`).text(formatCurrency(item.discountAmount));
    $(`#taxAmt-${index}`).text(formatCurrency(item.taxAmount || 0));
    $(`#lineTotal-${index}`).text(formatCurrency(item.total));

    syncAdditionalDiscountAmountFromPercent();
    updateItemsCount();
}

let pendingDeleteIndex = -1;

function showDeleteConfirm(index) {
    const name = saleItems[index].productName || saleItems[index].medicineName;
    document.getElementById('deleteConfirmMessage').textContent =
        `Are you sure to Delete the item "${name}"?`;
    pendingDeleteIndex = index;
    const modal = new bootstrap.Modal(document.getElementById('deleteConfirmModal'));
    modal.show();
}

document.getElementById('deleteConfirmYes').addEventListener('click', function () {
    if (pendingDeleteIndex >= 0) {
        const idx = pendingDeleteIndex;
        pendingDeleteIndex = -1;
        const modal = bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal'));
        if (modal) modal.hide();
        reallyRemoveItem(idx);
    }
});

function reallyRemoveItem(index) {
    const name = saleItems[index].productName || saleItems[index].medicineName;
    saleItems.splice(index, 1);
    recalculateBill();
    renderSaleItems();
    syncAdditionalDiscountAmountFromPercent();
    updateItemsCount();
    updateCompleteSaleBtn();
    showToast(`Removed ${name}`, 'warning');
}

function clearAllItems() {
    const ok = window.confirm('Clear all sale items?');
    if (!ok) return;
    saleItems = [];
    recalculateBill();
    renderSaleItems();
    $('#additionalDiscountPercent').val(0);
    $('#additionalDiscount').val(0);
    updateItemsCount();
    updateCompleteSaleBtn();
    showToast('Cart cleared', 'info');
}

// ============ BILL CALCULATION ============
function roundToNearestRupee(amount) {
    return Math.round((amount || 0) * 1) / 1;
}

function formatSignedCurrency(amount) {
    const n = parseFloat(amount || 0);
    if (n > 0) return '+ ' + formatCurrency(n);
    if (n < 0) return '- ' + formatCurrency(Math.abs(n));
    return formatCurrency(0);
}

function getAdditionalDiscountPercent() {
    const n = parseFloat($('#additionalDiscountPercent').val());
    if (!Number.isFinite(n)) return 0;
    return Math.min(100, Math.max(0, n));
}

function getAdditionalDiscountAmount() {
    const n = parseFloat($('#additionalDiscount').val());
    if (!Number.isFinite(n)) return 0;
    return Math.max(0, n);
}

function computeSubtotalBeforeAdditionalDiscount() {
    let total = 0;
    saleItems.forEach(item => {
        var net = parseFloat(item.itemNetAmount);
        if (Number.isFinite(net)) {
            total += net;
        } else {
            var mrp = parseFloat(item.mrp || item.price) || 0;
            var qty = parseFloat(item.quantity) || 0;
            var disc = parseFloat(item.discountAmount) || 0;
            total += Math.max(0, mrp * qty - disc);
        }
    });
    return Math.max(0, total);
}

function syncAdditionalDiscountAmountFromPercent() {
    var grossAmt = 0;
    saleItems.forEach(function (item) {
        var mrp = parseFloat(item.mrp || item.price) || 0;
        var qty = parseFloat(item.quantity) || 0;
        var discPct = parseFloat(item.discountPercent) || 0;
        var itemGross = mrp * qty;
        var itemDisc = itemGross * discPct / 100;
        grossAmt += (itemGross - itemDisc);
    });
    const percent = getAdditionalDiscountPercent();
    const amt = grossAmt * percent / 100;
    $('#additionalDiscountPercent').val(percent);
    $('#additionalDiscount').val(amt.toFixed(2));
}

function recalculateBill() {
    var billDiscPercent = parseFloat($('#additionalDiscountPercent').val()) || 0;

    if (!isPrefillingEditSale) {
        syncAdditionalDiscountAmountFromPercent();
    }

    const engineInput = saleItems.map(item => ({
        productName: item.productName,
        mrp: parseFloat(item.mrp || item.price) || 0,
        qty: parseFloat(item.quantity) || 0,
        gstPercent: parseFloat(item.taxPercent) || 0,
        itemDiscountType: 'PERCENT',
        itemDiscountValue: parseFloat(item.discountPercent) || 0
    }));
    const result = calculateInvoice(engineInput, billDiscPercent);

    result.items.forEach((engItem, i) => {
        saleItems[i].discountAmount = engItem.itemDiscountAmount;
        saleItems[i].itemNetAmount = engItem.itemNetAmount;
        saleItems[i].baseTotal = engItem.taxableAmount;
        saleItems[i].taxAmount = engItem.totalGSTAmount;
        saleItems[i].total = engItem.itemNetAmount;
        $(`#discAmt-${i}`).text(formatCurrency(engItem.itemDiscountAmount));
        $(`#taxAmt-${i}`).text(formatCurrency(engItem.totalGSTAmount));
        $(`#lineTotal-${i}`).text(formatCurrency(engItem.itemNetAmount));
    });

    var s = result.summary;
    var itemDiscTotal = 0;
    result.items.forEach(function (engItem) { itemDiscTotal += engItem.itemDiscountAmount; });
    $('#grsamt').text(formatCurrency(s.billGrossAmount));
    $('#afterDiscount').text(formatCurrency(s.netAmount));
    $('#billSubtotal').text(formatCurrency(s.taxableAmount));
    $('#billTaxAmount').text(formatCurrency(s.totalGSTAmount));
    $('#billItemDiscount').text('- ' + formatCurrency(itemDiscTotal));
    var rounded = roundToNearestRupee(s.netAmount);
    var roundOff = parseFloat((rounded - s.netAmount).toFixed(2));
    $('#billRoundOff').text(formatSignedCurrency(roundOff));
    $('#billGrandTotal').text(formatCurrency(rounded));

    if (isReturnMode) {
        const refund = getReturnRefundAmount();
        $('#billRefundAmount').text(formatCurrency(refund));
        $('#billRefundRow').show();
    } else {
        $('#billRefundRow').hide();
    }

    updateTaxBreakupUI();

    // Keep summary in sync with bill calculations
    updateItemsCount();

    // Update payment amount
    updatePaymentAmount(rounded);
}

$('#additionalDiscountPercent').on('input', debounce(function () {
    syncAdditionalDiscountAmountFromPercent();
    recalculateBill();
}, 200));

$('#saleItemsBody').on('input', '.item-qty-input', debounce(function () {
    const id = $(this).attr('id') || '';
    const match = id.match(/^qty-(\d+)$/);
    if (match) {
        updateItemQuantity(parseInt(match[1], 10), this.value);
    }
}, 200));

function updatePaymentAmount(grandTotal) {
    if (isReturnMode) {
        const refund = getReturnRefundAmount();
        $('#splitCash').val(refund.toFixed(2));
        return;
    }
    if (isPrefillingEditSale) {
        const c = parseFloat($('#splitCash').val()) || 0;
        const ca = parseFloat($('#splitCard').val()) || 0;
        const u = parseFloat($('#splitUpi').val()) || 0;
        const rem = Math.max(0, grandTotal - c - ca - u);
        $('#splitRemaining').text(formatCurrency(rem));
        return;
    }
    syncSplitPayments();
}

// ============ PAYMENT ============
function selectPaymentMethod(method, options) {
    if (isReturnMode && !isPrefillingEditSale) {
        return;
    }
    selectedPaymentMethod = 'Split';
    $('.payment-method-card').removeClass('selected');
    $('#pmSplit').addClass('selected');
    $('.payment-detail-form').removeClass('show');
    $('#paymentSplit').addClass('show');
    if (!options?.skipInitSplit) {
        initSplitPayments();
    }
    recalculateBill();
}

function getGrandTotal() {
    var billDiscPercent = parseFloat($('#additionalDiscountPercent').val()) || 0;
    if (!isPrefillingEditSale) {
        syncAdditionalDiscountAmountFromPercent();
    }
    var engineInput = saleItems.map(function (item) {
        return {
            productName: item.productName,
            mrp: parseFloat(item.mrp || item.price) || 0,
            qty: parseFloat(item.quantity) || 0,
            gstPercent: parseFloat(item.taxPercent) || 0,
            itemDiscountType: 'PERCENT',
            itemDiscountValue: parseFloat(item.discountPercent) || 0
        };
    });
    var result = calculateInvoice(engineInput, billDiscPercent);
    return roundToNearestRupee(result.summary.netAmount);
}

$('#splitCash').on('input', function () {
    syncSplitPayments('cash', false);
});

$('#splitCash').on('change blur', function () {
    syncSplitPayments('cash', true);
});

$('#splitCard').on('input', function () {
    syncSplitPayments('card', false);
});

$('#splitCard').on('change blur', function () {
    syncSplitPayments('card', true);
});

$('#splitUpi').on('input', function () {
    syncSplitPayments('upi', false);
});

$('#splitUpi').on('change blur', function () {
    syncSplitPayments('upi', true);
});

function initSplitPayments() {
    if (isSyncingSplit) return;
    isSyncingSplit = true;

    const grandTotal = getGrandTotal();
    $('#splitCash').val(grandTotal.toFixed(2));
    $('#splitCard').val((0).toFixed(2));
    $('#splitUpi').val((0).toFixed(2));
    $('#splitRemaining').text(formatCurrency(0));

    isSyncingSplit = false;
}

function syncSplitPayments(changedField, normalizeChangedField) {
    if (isSyncingSplit) return;

    isSyncingSplit = true;

    const grandTotal = getGrandTotal();

    let cash = parseFloat($('#splitCash').val()) || 0;
    let card = parseFloat($('#splitCard').val()) || 0;
    let upi = parseFloat($('#splitUpi').val()) || 0;

    cash = Math.max(0, Math.min(cash, grandTotal));
    card = Math.max(0, Math.min(card, grandTotal));
    upi = Math.max(0, Math.min(upi, grandTotal));

    // If the total exceeds grandTotal, cap the field the user is editing
    const totalEntered = cash + card + upi;
    if (totalEntered > grandTotal) {
        const excess = totalEntered - grandTotal;
        if (changedField === 'cash') cash = Math.max(0, cash - excess);
        else if (changedField === 'card') card = Math.max(0, card - excess);
        else if (changedField === 'upi') upi = Math.max(0, upi - excess);
        else {
            // fallback: reduce cash first
            const knockoff = Math.min(cash, excess);
            cash -= knockoff;
            const remain = excess - knockoff;
            if (remain > 0) card = Math.max(0, card - remain);
        }
    }

    const remaining = Math.max(0, grandTotal - cash - card - upi);

    if (normalizeChangedField) {
        if (changedField === 'cash') $('#splitCash').val(cash.toFixed(2));
        else if (changedField === 'card') $('#splitCard').val(card.toFixed(2));
        else if (changedField === 'upi') $('#splitUpi').val(upi.toFixed(2));
    }

    if (changedField !== 'cash') $('#splitCash').val(cash.toFixed(2));
    if (changedField !== 'card') $('#splitCard').val(card.toFixed(2));
    if (changedField !== 'upi') $('#splitUpi').val(upi.toFixed(2));

    $('#splitRemaining').text(formatCurrency(remaining));
    // Visual hint: green = settled, red = still due
    const remEl = $('#splitRemaining');
    if (remaining <= 0.01) {
        remEl.css('color', 'var(--accent-500)');
    } else if (remaining > 1) {
        remEl.css('color', 'var(--danger-500)');
    } else {
        remEl.css('color', 'var(--primary-600)');
    }

    isSyncingSplit = false;
}

// ============ ADVANCED SEARCH ============
function advancedSearch() {
    const batchNumber = $('#advBatchNumber').val().trim();
    const medicineName = $('#advMedicineName').val().trim();
    const composition = $('#advComposition').val().trim();
    const expiryFrom = $('#advExpiryFrom').val();
    const expiryTo = $('#advExpiryTo').val();

    let params = [];
    if (batchNumber) params.push(`batchNumber=${encodeURIComponent(batchNumber)}`);
    if (medicineName) params.push(`medicineName=${encodeURIComponent(medicineName)}`);
    if (composition) params.push(`composition=${encodeURIComponent(composition)}`);
    if (expiryFrom) params.push(`expiryFrom=${expiryFrom}`);
    if (expiryTo) params.push(`expiryTo=${expiryTo}`);

    if (params.length === 0) {
        showToast('Please enter at least one search criteria', 'warning');
        return;
    }

    fetch(`${MVC_BASE}/AdvancedBatchSearch?${params.join('&')}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(r => r.text())
        .then(html => {
            $('#advancedSearchResults').html(html);
        })
        .catch(() => {
            $('#advancedSearchResults').html('<div class="autocomplete-no-results" style="padding:20px;"><i class="bi bi-inbox" style="font-size:2rem;display:block;margin-bottom:8px;"></i> No batches found</div>');
        });
}

// ============ COMPLETE SALE ============
function updateCompleteSaleBtn() {
    const canComplete = saleItems.length > 0;
    $('#completeSaleBtn').prop('disabled', !canComplete);
}

function isPaymentSet() {
    const cash = parseFloat($('#splitCash').val()) || 0;
    const card = parseFloat($('#splitCard').val()) || 0;
    const upi = parseFloat($('#splitUpi').val()) || 0;
    return (cash + card + upi) > 0;
}

function showPaymentAlertModal() {
    const modal = new bootstrap.Modal(document.getElementById('paymentAlertModal'));
    modal.show();
}

function completeSale() {
    if (saleItems.length === 0) {
        showToast('Please add at least one item', 'error');
        return;
    }
    if (isReturnMode) {
        executeSaleSubmit();
        return;
    }
    if (!isPaymentSet()) {
        showPaymentAlertModal();
        return;
    }
    validateStockBeforeConfirm();
}

function confirmExpiryBadge(item) {
    if (!item.expiryDate) return '';
    const expiry = new Date(item.expiryDate);
    const today = new Date(); today.setHours(0,0,0,0);
    const days = Math.ceil((expiry - today) / (1000*60*60*24));
    if (days < 0) return '<span class="confirm-badge expired">EXP</span>';
    if (days <= 90) return '<span class="confirm-badge warning">EXP</span>';
    return '';
}
function confirmStockBadge(item) {
    const qty = parseFloat(item.availableQty) || 0;
    if (qty <= 0) return '<span class="confirm-badge out">OOS</span>';
    if (qty <= 5) return '<span class="confirm-badge low">LOW</span>';
    return '';
}
function confirmDiscountBadge(item) {
    const d = parseFloat(item.discountPercent) || 0;
    if (d > 25) return `<span class="confirm-badge discount">${d}% OFF</span>`;
    return '';
}
function showSaleConfirmModal() {
    const billDiscPercent = parseFloat($('#additionalDiscountPercent').val()) || 0;
    const engineInput = saleItems.map(item => ({
        productName: item.productName,
        mrp: parseFloat(item.mrp || item.price) || 0,
        qty: parseFloat(item.quantity) || 0,
        gstPercent: parseFloat(item.taxPercent) || 0,
        itemDiscountType: 'PERCENT',
        itemDiscountValue: parseFloat(item.discountPercent) || 0
    }));
    const result = calculateInvoice(engineInput, billDiscPercent);
    const s = result.summary;
    var itemDiscTotal = 0;
    result.items.forEach(function (engItem) { itemDiscTotal += engItem.itemDiscountAmount; });
    const rounded = roundToNearestRupee(s.netAmount);
    const roundOff = parseFloat((rounded - s.netAmount).toFixed(2));
    const grandTotal = rounded;

    const cust = selectedCustomer;
    const fallbackName = ($('#newCustName').val() || '').trim();
    const fallbackPhone = ($('#newCustPhone').val() || '').trim();
    const custName = cust?.name || fallbackName || 'Walk-in Customer';
    const custPhone = cust?.phone || fallbackPhone || '-';

    let html = '';
    html += `<div class="confirm-customer">${custName} &nbsp;|&nbsp; ${custPhone}</div>`;

    html += `<table class="modal-items-table"><thead><tr>
        <th>#</th><th>Product</th><th>Batch</th><th>Qty</th><th>Rate</th><th>Disc%</th><th>Tax%</th><th>Total</th>
    </tr></thead><tbody>`;
    saleItems.forEach((item, i) => {
        const engItem = result.items[i];
        const lineTotal = engItem ? engItem.finalAmount : ((parseFloat(item.price) || 0) * (parseFloat(item.quantity) || 0));
        html += `<tr>
            <td>${i + 1}</td>
            <td>${item.productName || ''}${confirmExpiryBadge(item)}${confirmStockBadge(item)}${confirmDiscountBadge(item)}</td>
            <td style="font-size:0.75rem;color:var(--gray-500)">${item.batchNumber || ''}</td>
            <td>${item.quantity}</td>
            <td>${formatCurrency(item.price)}</td>
            <td>${item.discountPercent || 0}%</td>
            <td>${item.taxPercent || 0}%</td>
            <td style="font-weight:600">${formatCurrency(lineTotal)}</td>
        </tr>`;
    });
    html += `</tbody></table>`;

    html += `<div class="confirm-summary">
        <span>Items: <strong>${saleItems.length}</strong></span>
        <span>Taxable: <strong>${formatCurrency(s.taxableAmount)}</strong></span>
        <span>Item Disc: <strong>-${formatCurrency(itemDiscTotal)}</strong></span>
        <span>Tax: <strong>${formatCurrency(s.totalGSTAmount)}</strong></span>
    </div>`;

    const splitCash = parseFloat($('#splitCash').val()) || 0;
    const splitCard = parseFloat($('#splitCard').val()) || 0;
    const splitUpi = parseFloat($('#splitUpi').val()) || 0;
    let payParts = [];
    if (splitCash > 0) payParts.push(`Cash: ${formatCurrency(splitCash)}`);
    if (splitCard > 0) payParts.push(`Card: ${formatCurrency(splitCard)}`);
    if (splitUpi > 0) payParts.push(`UPI: ${formatCurrency(splitUpi)}`);
    const paySummary = payParts.length > 0 ? payParts.join(' + ') : 'Not set';
    html += `<div class="confirm-footer-row">
        <span>Addl Disc: ${formatCurrency(s.billDiscountAmount)} &nbsp;|&nbsp; Round: ${formatCurrency(roundOff)}</span>
        <span>Payment: <strong>${paySummary}</strong></span>
    </div>`;
    html += `<div class="confirm-grand-total">Grand Total: ${formatCurrency(grandTotal)}</div>`;

    $('#saleConfirmBody').html(html);
    const modal = new bootstrap.Modal(document.getElementById('saleConfirmModal'));
    modal.show();
}

document.getElementById('saleConfirmYes').addEventListener('click', function () {
    const modal = bootstrap.Modal.getInstance(document.getElementById('saleConfirmModal'));
    if (modal) modal.hide();
    executeSaleSubmit();
});

function executeSaleSubmit() {
    const grandTotal = isReturnMode ? getReturnRefundAmount() : getGrandTotal();
    let payments = [];

    if (isReturnMode) {
        payments = [{ paymentMode: 'Cash', amount: parseFloat(grandTotal.toFixed(2)), reference: null }];
    } else {
        const cash = parseFloat($('#splitCash').val()) || 0;
        const card = parseFloat($('#splitCard').val()) || 0;
        const upi = parseFloat($('#splitUpi').val()) || 0;
        if (Math.abs((cash + card + upi) - grandTotal) > 0.01) {
            showToast('Split payment total does not match grand total', 'error');
            return;
        }
        const splitCardRefNo = ($('#splitCardRefNo').val() || '').trim();
        const splitUpiRefNo = ($('#splitUpiRefNo').val() || '').trim();

        const cardReference = splitCardRefNo || null;
        const upiReference = splitUpiRefNo || null;

        if (cash > 0) payments.push({ paymentMode: 'Cash', amount: parseFloat(cash.toFixed(2)), reference: null });
        if (card > 0) payments.push({ paymentMode: 'Card', amount: parseFloat(card.toFixed(2)), reference: cardReference });
        if (upi > 0) payments.push({ paymentMode: 'UPI', amount: parseFloat(upi.toFixed(2)), reference: upiReference });
    }

    const fallbackName = ($('#newCustName').val() || '').trim() || null;
    const fallbackPhone = ($('#newCustPhone').val() || '').trim() || null;
    const searchText = ($('#customerSearch').val() || '').trim();
    const normalizedSearch = searchText.replace(/[\s\-\+\(\)]/g, '');
    const inferredSearchPhone = /^\d{4,}$/.test(normalizedSearch) ? normalizedSearch : null;
    const inferredSearchName = inferredSearchPhone ? null : (searchText.length >= 2 ? searchText : null);

    let roundOff = 0;
    if (!isReturnMode) {
        var engInput = saleItems.map(function (i) {
            return {
                productName: i.productName,
                mrp: parseFloat(i.mrp || i.price) || 0,
                qty: parseFloat(i.quantity) || 0,
                gstPercent: parseFloat(i.taxPercent) || 0,
                itemDiscountType: 'PERCENT',
                itemDiscountValue: parseFloat(i.discountPercent) || 0
            };
        });
        var billPct = parseFloat($('#additionalDiscountPercent').val()) || 0;
        var engResult = calculateInvoice(engInput, billPct);
        var netAmt = engResult.summary.netAmount;
        var rounded = roundToNearestRupee(netAmt);
        roundOff = parseFloat((rounded - netAmt).toFixed(2));
        // sync item fields from engine
        engResult.items.forEach(function (engItem, idx) {
            if (saleItems[idx]) {
                saleItems[idx].discountAmount = engItem.itemDiscountAmount;
                saleItems[idx].itemNetAmount = engItem.itemNetAmount;
                saleItems[idx].baseTotal = engItem.taxableAmount;
                saleItems[idx].taxAmount = engItem.totalGSTAmount;
                saleItems[idx].total = engItem.finalAmount;
            }
        });
    }

    const request = {
        saleId: editingSaleId || null,
        returnMode: isReturnMode === true,
        customerId: selectedCustomer?.id || null,
        customerName: selectedCustomer?.name || fallbackName || inferredSearchName,
        customerPhone: selectedCustomer?.phone || fallbackPhone || inferredSearchPhone,
        items: saleItems.map(i => ({
            productId: i.productId,
            productName: i.productName,
            batchNumber: i.batchNumber,
            expiryDate: i.expiryDate,
            uomName: i.uomName,
            saleUomName: i.saleUomName || i.uomName,
            unitPrice: i.price,
            quantity: i.quantity,
            unitType: i.unitType,
            discountPercent: i.discountPercent,
            taxPercent: parseFloat(i.taxPercent) || 0
        })),
        additionalDiscount: parseFloat($('#additionalDiscount').val()) || 0,
        roundOff: roundOff,
        payments: payments
    };

    // Disable button and show loading
    $('#completeSaleBtn').prop('disabled', true).html('<span class="spinner-pharmacy"></span> Processing...');

    const formData = new FormData(document.getElementById('completeSaleForm'));
    formData.set('SaleJson', JSON.stringify(request));

    fetch('/Sales/CompleteSale', {
        method: 'POST',
        body: formData
    })
        .then(response => response.text())
        .then(html => {
            const parser = new DOMParser();
            const doc = parser.parseFromString(html, 'text/html');

            const errorInput = doc.querySelector('#serverSaleError');
            const successInput = doc.querySelector('#serverSaleSuccessInvoice');
            const saleIdInput = doc.querySelector('#serverSaleSuccessId');
            const uniqueIdInput = doc.querySelector('#serverSaleSuccessUniqueId');

            if (errorInput && errorInput.value) {
                showToast(errorInput.value, 'error');
                $('#completeSaleBtn').prop('disabled', false).html('<i class="bi bi-check-circle"></i> Complete Sale');
                return;
            }

            if (successInput && successInput.value) {
                const invoice = successInput.value;
                const saleId = parseInt(saleIdInput?.value || '', 10) || null;
                const uniqueId = uniqueIdInput?.value || null;
                const isReturnInput = doc.querySelector('#serverSaleSuccessIsReturn');
                const isReturn = isReturnInput?.value === 'true';

                $('#successInvoice').text(`#${invoice}`);
                $('#successOverlay').addClass('show');

                window.__lastCompletedSaleId = saleId;
                window.__lastCompletedSaleUniqueId = uniqueId;
                window.__lastCompletedSaleIsReturn = isReturn;
                const $printBtn = $('#printBillBtn');
                if (saleId && $printBtn.length) {
                    $printBtn.show();
                }

                saleItems = [];
                editingSaleId = null;
                isReturnMode = false;
                originalSaleItemsByKey = null;
                originalSaleItemsByPidBnKey = null;
                allowedReturnItemKeys = null;
                renderSaleItems();
                recalculateBill();
                updateCompleteSaleBtn();

                $('#completeSaleBtn').prop('disabled', false).html('<i class="bi bi-check-circle"></i> Complete Sale');
                return;
            }

            showToast('Unexpected server response', 'error');
            $('#completeSaleBtn').prop('disabled', false).html('<i class="bi bi-check-circle"></i> Complete Sale');
        })
        .catch(err => {
            showToast('Network error: ' + err.message, 'error');
            $('#completeSaleBtn').prop('disabled', false).html('<i class="bi bi-check-circle"></i> Complete Sale');
        });
}

// Show server result after redirect
$(function () {
    const invoice = ($('#serverSaleSuccessInvoice').val() || '').toString();
    const saleIdRaw = ($('#serverSaleSuccessId').val() || '').toString();
    const saleId = parseInt(saleIdRaw, 10) || null;
    const uniqueIdRaw = ($('#serverSaleSuccessUniqueId').val() || '').toString();
    const uniqueId = uniqueIdRaw || null;
    const isReturnRaw = ($('#serverSaleSuccessIsReturn').val() || '').toString();
    const isReturn = isReturnRaw === 'true';
    const err = ($('#serverSaleError').val() || '').toString();
    if (err) {
        showToast(err, 'error');
    }
    if (invoice) {
        $('#successInvoice').text(`#${invoice}`);
        $('#successOverlay').addClass('show');

        window.__lastCompletedSaleId = saleId;
        window.__lastCompletedSaleUniqueId = uniqueId;
        window.__lastCompletedSaleIsReturn = isReturn;
        if (saleId && $('#printBillBtn').length) {
            $('#printBillBtn').show();
        }
    }
});

function printLastBill() {
    const billType = window.__lastCompletedSaleIsReturn ? 'SalesReturn' : 'Sale';
    const uid = window.__lastCompletedSaleUniqueId;
    if (uid) {
        window.open(`/Bill/Print?billType=${billType}&id=${encodeURIComponent(uid)}`, '_blank');
    } else {
        const saleId = window.__lastCompletedSaleId;
        if (saleId) window.open(`/Bill/Print?billType=${billType}&id=${encodeURIComponent(saleId)}`, '_blank');
    }
}

function validateStockBeforeConfirm() {
    var items = saleItems.map(function (i) {
        return {
            productId: i.productId,
            batchNumber: i.batchNumber,
            quantity: i.quantity,
            uomName: i.saleUomName || i.uomName || 'PCS'
        };
    });

    $.ajax({
        url: API_BASE + '/sales/validate-stock',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ items: items }),
        success: function (result) {
            if (result && result.hasIssues) {
                showStockIssueModal(result.issues || []);
            } else {
                showSaleConfirmModal();
            }
        },
        error: function () {
            showSaleConfirmModal();
        }
    });
}

function showStockIssueModal(issues) {
    var html = '<div style="margin-bottom:12px;color:var(--gray-600);font-size:0.85rem;">The following items have insufficient stock. Please adjust quantities or remove them.</div>';
    html += '<table class="stock-issue-table"><thead><tr>';
    html += '<th>Product</th><th>Batch</th><th>Available</th><th>Required</th>';
    html += '</tr></thead><tbody>';
    issues.forEach(function (issue) {
        html += '<tr>' +
            '<td><strong>' + (issue.productName || '') + '</strong></td>' +
            '<td>' + (issue.batchNumber || '') + '</td>' +
            '<td class="stock-issue-available">' + formatNumber(issue.availableQty) + '</td>' +
            '<td class="stock-issue-required">' + formatNumber(issue.requiredQty) + '</td>' +
        '</tr>';
    });
    html += '</tbody></table>';
    $('#stockIssueBody').html(html);
    var modal = new bootstrap.Modal(document.getElementById('stockIssueModal'));
    modal.show();
}

function startNewSale() {
    selectedCustomer = null;
    saleItems = [];
    currentBatchInfo = null;
    selectedUnitType = 'PCS';
    selectedPaymentMethod = 'Split';
    editingSaleId = null;
    isReturnMode = false;
    originalSaleItemsByKey = null;
    originalSaleItemsByPidBnKey = null;
    allowedReturnItemKeys = null;
    originalSaleAdditionalDiscount = 0;
    window.__lastCompletedSaleId = null;
    window.__lastCompletedSaleUniqueId = null;
    window.__lastCompletedSaleIsReturn = false;
    isPrefillingEditSale = false;
    pendingDeleteIndex = -1;

    $('#editInvoiceBadge').hide();
    $('#editInvoiceNumber').text('');
    document.title = 'New Sale';

    // Reset UI
    $('#selectedCustomerCard').hide().empty();
    $('#customerSearch').val('').show();
    $('#customerSearchIcon').show();
    $('#toggleNewCustomerForm').show();
    $('#skipCustomerBtn').show();
    $('#newCustomerForm').removeClass('show');
    $('#newCustName, #newCustPhone').val('');

    $('#batchSearch, #medicineSearch').val('');
    $('#batchInfoCard, #medicineBatchesCard').removeClass('show').empty();
    $('#advancedSearchResults').empty();
    $('#advBatchNumber, #advMedicineName, #advComposition, #advExpiryFrom, #advExpiryTo').val('');

    $('#additionalDiscountPercent').val(0);
    $('#additionalDiscount').val(0);
    recalculateBill();
    renderSaleItems();

    // Reset payment
    selectPaymentMethod('Split');
    $('#pmSplit').show();
    $('.payment-method-card').css('pointer-events', '');
    $('#splitCash, #splitCard, #splitUpi').val('');
    $('#splitCardRefNo, #splitUpiRefNo').val('');
    $('#splitRemaining').text(formatCurrency(0));

    $('#completeSaleBtn').prop('disabled', true).html('<i class="bi bi-check-circle"></i> Complete Sale');
    $('#successOverlay').removeClass('show');
    if ($('#printBillBtn').length) {
        $('#printBillBtn').hide();
    }

    updateCompleteSaleBtn();
    switchMedicineTab('direct');
}

// ============ CLICK OUTSIDE TO CLOSE DROPDOWNS ============
$(document).on('click', function (e) {
    if (!$(e.target).closest('.input-group-pharmacy').length) {
        $('.autocomplete-dropdown').removeClass('show');
    }
});

// ============ KEYBOARD SHORTCUTS ============
$(document).on('keydown', function (e) {
    // F2 - Focus batch search
    if (e.key === 'F2') {
        e.preventDefault();
        switchMedicineTab('batch');
        $('#batchSearch').focus();
    }
    // F3 - Focus medicine search
    if (e.key === 'F3') {
        e.preventDefault();
        switchMedicineTab('direct');
        $('#medicineSearch').focus();
    }
    // F4 - Focus customer search
    if (e.key === 'F4') {
        e.preventDefault();
        $('#customerSearch').focus();
    }
    // Escape - Close overlays
    if (e.key === 'Escape') {
        $('.autocomplete-dropdown').removeClass('show');
        $('#newCustomerForm').removeClass('show');
    }
});

// ============ INIT ============
$(document).ready(function () {
    if (!Array.isArray(window.__prefetchStocks)) {
        safeNotify('Prefetch missing: window.__prefetchStocks is not an array');
    } else if (!PREFETCH_STOCKS.length) {
        safeNotify('Prefetch empty: 0 stock rows loaded');
    }
    updateCompleteSaleBtn();
    recalculateBill();

    switchMedicineTab('direct');

    const params = new URLSearchParams(window.location.search);
    const editSaleId = params.get('editSaleId');
    const returnModeParam = params.get('returnMode');
    isReturnMode = returnModeParam === '1' || String(returnModeParam || '').toLowerCase() === 'true';
    if (editSaleId) {
        loadSaleForEdit(editSaleId);
    }
});

function applyReturnModeLocks() {
    if (!isReturnMode) return;

    // Customer
    $('#customerSearch').prop('disabled', true).hide();
    $('#toggleNewCustomerForm').prop('disabled', true).hide();
    $('#skipCustomerBtn').prop('disabled', true).hide();
    $('#selectedCustomerCard .customer-remove').hide();

    // Additional discount
    //$('#additionalDiscountPercent').prop('disabled', true);
    //$('#additionalDiscount').prop('disabled', true);

    // Return mode - show refund amount in Cash field
    $('.payment-method-card').css('pointer-events', 'none');
    $('.payment-detail-form').removeClass('show');
    $('#paymentSplit').addClass('show');
    $('#splitCash').prop('readonly', true);
    $('#splitCard').prop('readonly', true);
    $('#splitUpi').prop('readonly', true);
    $('#splitCard').closest('.col-md-4').hide();
    $('#splitUpi').closest('.col-md-4').hide();
    $('#splitCardRefNo').closest('div').hide();
    $('#splitUpiRefNo').closest('div').hide();

    // Prevent add/remove/clear
    /* $('#clearCartBtn').hide();*/

    // Prevent adding medicines
    //$('#medicineSearch, #batchSearch, #advBatchNumber, #advMedicineName, #advComposition, #advExpiryFrom, #advExpiryTo').prop('disabled', true);
    //$('#medicineDropdown, #batchDropdown').removeClass('show').empty();
    //$('#medicineBatchesCard, #batchInfoCard').removeClass('show').empty();
    //$('#advancedSearchResults').empty();
    //$('#tabDirect, #tabBatch, #tabAdvanced').prop('disabled', true).css('pointer-events', 'none');
}

function loadSaleForEdit(saleId) {
    fetch(`${MVC_BASE}/GetSaleForEdit?id=${encodeURIComponent(saleId)}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(r => {
            if (!r.ok) throw new Error('Failed to load sale');
            return r.json();
        })
        .then(data => {
            if (!data) throw new Error('Sale data missing');

            isPrefillingEditSale = true;

            editingSaleId = parseInt(data.saleId || saleId, 10) || null;

            const invNo = data.invoiceNumber || '';
            if (invNo) {
                $('#editInvoiceNumber').text(invNo);
                $('#editInvoiceBadge').show();
                document.title = 'Editing: ' + invNo + ' - New Sale';
            }

            // Reset current draft
            selectedCustomer = null;
            saleItems = [];
            currentBatchInfo = null;
            selectedUnitType = 'PCS';

            // Customer
                if (data.customer && data.customer.id) {
                    selectedCustomer = { id: data.customer.id, name: data.customer.name || '', phone: data.customer.phone || '', customerCode: data.customer.customerCode || '' };
                    $('#selectedCustomerCard').html(`
                        <div class="customer-card" style="position: relative;" data-customer-json='${JSON.stringify(selectedCustomer).replace(/'/g, "&#39;")}'>
                            <button class="customer-remove-top" onclick="removeCustomer()" title="Remove Customer">
                                <i class="bi bi-x-lg"></i>
                            </button>
                            <div class="customer-avatar">
                                <i class="bi bi-person"></i>
                            </div>
                            <div class="customer-info">
                                <div class="customer-name">${selectedCustomer.name}${selectedCustomer.customerCode ? '<span style="font-weight:400;font-size:0.78rem;color:var(--gray-500);margin-left:8px;">OPD: ' + selectedCustomer.customerCode + '</span>' : ''}</div>
                                <div class="customer-phone"><i class="bi bi-telephone"></i> ${selectedCustomer.phone}</div>
                            </div>
                        </div>
                    `).show();
                $('#customerSearch').hide();
                $('#customerSearchIcon').hide();
                $('#toggleNewCustomerForm').hide();
                $('#skipCustomerBtn').hide();
            } else {
                $('#selectedCustomerCard').hide().empty();
                $('#customerSearch').show();
                $('#customerSearchIcon').show();
                $('#toggleNewCustomerForm').show();
                $('#skipCustomerBtn').show();
            }

            // Items — always populate original item references for return mode validation
            {
                const items = data.items || [];
                originalSaleItemsByKey = {};
                originalSaleItemsByPidBnKey = {};
                allowedReturnItemKeys = new Set();
                items.forEach(i => {
                    const qty = parseFloat(i.quantity) || 0;
                    const price = parseFloat(i.price) || 0;
                    const originalLineTotal = parseFloat(i.total);
                    const key = makeSaleItemKeyFromFields(i.productId, i.batchNumber, i.expiryDate);
                    const pidBnKey = `${String(i.productId || '').trim().toLowerCase()}|${String(i.batchNumber || '').trim().toLowerCase()}`;
                    originalSaleItemsByKey[key] = {
                        qty: qty,
                        baseQty: qty,
                        uomName: i.uomName || 'PCS',
                        saleUomName: i.uomName || 'PCS',
                        total: Number.isFinite(originalLineTotal) ? originalLineTotal : (price * qty)
                    };
                    if (!originalSaleItemsByPidBnKey[pidBnKey]) {
                        originalSaleItemsByPidBnKey[pidBnKey] = originalSaleItemsByKey[key];
                    }
                    allowedReturnItemKeys.add(pidBnKey);
                });
            }

            if (!isReturnMode) {
                const items = data.items || [];
                saleItems = [];
                originalSaleAdditionalDiscount = parseFloat(data.additionalDiscount) || parseFloat(data.additionalDiscountAmount) || 0;
                items.forEach(i => {
                    const stock = findStockByProductBatch(i.productId, i.batchNumber) || findStockByProductId(i.productId);
                    const resolvedName = readStockProductName(stock) || '';
                    const resolvedUom = readStockUom(stock) || 'PCS';
                    const resolvedTaxPercent = detectGstPercentFromTaxName(readStockTaxName(stock));

                    const qty = parseFloat(i.quantity) || 0;
                    const price = parseFloat(i.price) || 0;
                    const originalLineTotal = parseFloat(i.total);

                    const serverDiscPercent = parseFloat(i.discountPercent) || 0;
                    const serverDiscAmount = parseFloat(i.discountAmount) || 0;
                    const lineTotal = price * qty;
                    const derivedDiscPercent = (serverDiscPercent > 0)
                        ? serverDiscPercent
                        : (lineTotal > 0 && serverDiscAmount > 0) ? (serverDiscAmount / lineTotal) * 100 : 0;

                    const serverTaxPercent = parseFloat(i.taxPercent);
                    const effectiveTaxPercent = Number.isFinite(serverTaxPercent) && serverTaxPercent > 0
                        ? serverTaxPercent
                        : (parseFloat(resolvedTaxPercent) || 0);

                    const item = {
                        productId: i.productId,
                        productName: (i.productName && String(i.productName).trim()) ? i.productName : resolvedName,
                        batchNumber: i.batchNumber,
                        expiryDate: i.expiryDate,
                        uomName: (i.uomName && String(i.uomName).trim()) ? i.uomName : resolvedUom,
                        saleUomName: (i.uomName && String(i.uomName).trim()) ? i.uomName : resolvedUom,
                        quantity: qty,
                        unitType: i.unitType || (i.uomName || 'PCS'),
                        price: price,
                        mrp: parseFloat(i.mrp) || 0,
                        discountPercent: derivedDiscPercent,
                        discountAmount: parseFloat(i.discountAmount) || 0,
                        taxPercent: effectiveTaxPercent,
                        taxAmount: parseFloat(i.taxAmount) || 0,
                        baseTotal: parseFloat(i.total) || 0,
                        total: parseFloat(i.total) || 0,
                        availableQty: i.availableQty
                    };
                    saleItems.push(item);
                });

                // Additional discount
                const addDisc = parseFloat(data.additionalDiscount) || 0;
                $('#additionalDiscount').val(addDisc.toFixed(2));
                // back-calc percent from gross amount (sum of item nets)
                var grossAmt = 0;
                saleItems.forEach(function (item) {
                    var mrp = parseFloat(item.mrp || item.price) || 0;
                    var qty = parseFloat(item.quantity) || 0;
                    var discPct = parseFloat(item.discountPercent) || 0;
                    var itemGross = mrp * qty;
                    var itemDisc = itemGross * discPct / 100;
                    grossAmt += (itemGross - itemDisc);
                });
                const percent = grossAmt > 0 ? (addDisc / grossAmt) * 100 : 0;
                // Keep more precision to avoid amount drift after subsequent recalculations
                $('#additionalDiscountPercent').val(percent.toFixed(2));
            }

            // Payment prefill - always use Split
            selectPaymentMethod('Split', { skipInitSplit: true });
            const p = data.payment || {};

            const splitCash = (p.splitCash !== undefined && p.splitCash !== null) ? p.splitCash : (p.cashReceived || 0);
            const splitCard = (p.splitCard !== undefined && p.splitCard !== null) ? p.splitCard : (p.cardAmount || 0);
            const splitUpi = (p.splitUpi !== undefined && p.splitUpi !== null) ? p.splitUpi : (p.upiAmount || 0);
            $('#splitCash').val(parseFloat(splitCash).toFixed(2));
            $('#splitCard').val(parseFloat(splitCard).toFixed(2));
            $('#splitUpi').val(parseFloat(splitUpi).toFixed(2));
            $('#splitCardRefNo').val(p.splitCardRefNo || p.cardRefNo || '');
            $('#splitUpiRefNo').val(p.splitUpiRefNo || p.upiRefNo || '');

            const gt = getGrandTotal();
            const c = parseFloat($('#splitCash').val()) || 0;
            const ca = parseFloat($('#splitCard').val()) || 0;
            const u = parseFloat($('#splitUpi').val()) || 0;
            const rem = Math.max(0, gt - c - ca - u);
            $('#splitRemaining').text(formatCurrency(rem));

            if (!isReturnMode) {
                renderSaleItems();
                recalculateBill();
                updateCompleteSaleBtn();
                switchMedicineTab('direct');
            }

            applyReturnModeLocks();

            isPrefillingEditSale = false;
        })
        .catch(err => {
            isPrefillingEditSale = false;
            showToast(err?.message || 'Failed to load sale', 'error');
        });
}

