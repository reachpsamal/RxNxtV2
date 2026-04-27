// =============================================
// PharmaCare POS - Sales Management JavaScript
// =============================================

// ============ STATE ============
let selectedCustomer = null;
let saleItems = [];
let selectedPaymentMethod = 'Cash';
let currentBatchInfo = null;
let editingSaleId = null;
let isPrefillingEditSale = false;
let selectedUnitType = 'PCS';
let debounceTimer = null;
let suppressBatchAutoSelectUntil = 0;
let isSyncingSplit = false;
let lastBatchQuickAdd = { key: null, at: 0 };
let inFlightBatchAdds = new Map();

const MVC_BASE = '/Sales';

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
                        email: obj.email ?? obj.Email ?? null
                    } : null;
                } catch { selectedCustomer = null; }
            }
            $('#customerDropdown').removeClass('show').empty();
            $('#customerSearch').val('').hide();
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
    selectedCustomer = null;
    $('#selectedCustomerCard').hide().empty();
    $('#customerSearch').val('').show();
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
        <div class="customer-card" style="background: var(--gray-50); border-color: var(--gray-200);">
            <div class="customer-avatar" style="background: var(--gray-400);">
                <i class="bi bi-person" style="font-weight: normal;"></i>
            </div>
            <div class="customer-info">
                <div class="customer-name">Walk-in Customer</div>
                <div class="customer-phone">No loyalty tracking</div>
            </div>
            <button class="customer-remove" onclick="removeCustomer()" title="Change">
                <i class="bi bi-pencil"></i>
            </button>
        </div>
    `).show();
    $('#customerSearch').hide();
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
        onDone([]);
        return;
    }

    const results = PREFETCH_STOCKS
        .filter(s => (s?.batchNumber || '').toString().toLowerCase().includes(query.toLowerCase()))
        .sort((a, b) => String(a?.batchNumber || '').localeCompare(String(b?.batchNumber || ''), 'en', { sensitivity: 'base' }))
        .slice(0, 20);

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

    const html = items.map(b => {
        const isExpired = !!b.isExpired;
        const isNearExpiry = !!b.isNearExpiry;
        const expiryClass = isExpired ? 'badge-expired' : (isNearExpiry ? 'badge-expiry-warning' : 'badge-stock');
        const expiryText = isExpired ? 'EXPIRED' : (isNearExpiry ? 'Near Expiry' : 'Valid');
        const exp = b.expiryDate ? new Date(b.expiryDate) : null;
        const expTxt = exp && !Number.isNaN(exp.getTime())
            ? exp.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })
            : '-';

        return `
            <div class="autocomplete-item" data-product-id="${b.productId}" data-batch-number="${String(b.batchNumber || '')}" onclick="addFromBatchSelection('${b.productId}', '${String(b.batchNumber || '').replace(/'/g, "\\'")}')">
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
$('#medicineSearch').on('input', debounce(function () {
    const q = $(this).val().trim();
    if (q.length < 2) {
        $('#medicineDropdown').removeClass('show').empty();
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

    const dropdown = $('#medicineDropdown');
    if (!results.length) {
        dropdown
            .html('<div class="autocomplete-no-results"><i class="bi bi-capsule"></i> No medicine found</div>')
            .addClass('show');
        return;
    }

    const html = results.map(m => {
        const manufacturer = (m.manufacturer || '').trim();
        const manufacturerPrefix = manufacturer ? `${manufacturer} · ` : '';
        return `
            <div class="autocomplete-item" onclick="addFromDirectSelection('${m.productId}', '${String(m.batchNumber || '').replace(/'/g, "\\'")}')">
                <div class="item-name">${String(m.productName || '')}</div>
                <div class="item-detail">
                    ${manufacturerPrefix}Batch: <strong>${String(m.batchNumber || '')}</strong> &middot; Stock: ${formatNumber(m.availableQty)} ${String(m.uomName || '')}
                </div>
            </div>
        `;
    }).join('');

    dropdown.html(html).addClass('show');
}));

// ============ CART MANAGEMENT ============
function addToCart() {
    if (!currentBatchInfo) { showToast('Please select a batch first', 'warning'); return; }

    const qty = parseInt($('#addQty').val()) || 0;
    const unitType = selectedUnitType;

    if (qty <= 0) { showToast('Please enter a valid quantity', 'error'); return; }

    const maxQty = readStockQty(currentBatchInfo);
    if (qty > maxQty) { showToast(`Only ${maxQty} ${unitType.toLowerCase()}s available`, 'error'); return; }

    // Check if already in cart
    const existingIndex = saleItems.findIndex(i => i.productId === currentBatchInfo.productId && i.batchNumber === currentBatchInfo.batchNumber && i.uomName === readStockUom(currentBatchInfo));

    if (existingIndex >= 0) {
        saleItems[existingIndex].quantity = qty;
        saleItems[existingIndex].unitType = unitType;
        saleItems[existingIndex].price = readStockMrp(currentBatchInfo);
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
            quantity: qty,
            unitType: unitType,
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
    updateSaleItemsSummary();
    updateCompleteSaleBtn();

    // Clear batch selection
    currentBatchInfo = null;
    selectedUnitType = 'PCS';
    $('#batchInfoCard, #medicineBatchesCard').removeClass('show').empty();
    $('#batchSearch, #medicineSearch').val('');
}

function addFromAdvancedSearch(productId, batchNumber) {
    const addKey = `${String(productId || '').trim().toLowerCase()}|${String(batchNumber || '').trim().toLowerCase()}`;
    const now = Date.now();
    const lastInFlightAt = inFlightBatchAdds.get(addKey);
    if (lastInFlightAt && (now - lastInFlightAt) < 1200) {
        return;
    }
    inFlightBatchAdds.set(addKey, now);

    try {
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

        currentBatchInfo = b;

        const fixedUnit = readStockUom(b);
        selectedUnitType = fixedUnit;

        const availableQty = readStockQty(b);
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
            saleItems[existingIndex].price = readStockMrp(currentBatchInfo);
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
                quantity: 1,
                unitType: fixedUnit,
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
        renderSaleItems();
        // Quick-add changes subtotal; keep additional discount amount aligned
        syncAdditionalDiscountAmountFromPercent();
        recalculateBill();
        updateSaleItemsSummary();
        updateCompleteSaleBtn();
    } finally {
        // Allow re-add after local operation finishes; quick-add is still protected by timestamp guard
        inFlightBatchAdds.delete(addKey);
    }
}

function recalculateItem(index) {
    const item = saleItems[index];
    const price = parseFloat(item.price) || 0;
    const qty = parseFloat(item.quantity) || 0;
    const discPercent = parseFloat(item.discountPercent) || 0;
    const taxPercent = parseFloat(item.taxPercent) || 0;

    const lineTotal = price * qty;
    item.discountAmount = lineTotal * discPercent / 100;
    const afterDisc = Math.max(0, lineTotal - (parseFloat(item.discountAmount) || 0));
    item.taxPercent = taxPercent;
    item.baseTotal = afterDisc;
    const includedTax = taxPercent > 0 ? (afterDisc * (taxPercent / (100 + taxPercent))) : 0;
    item.taxAmount = includedTax;
    item.total = afterDisc;
}

function computeTaxBreakupBySlab() {
    const result = {
        5: { cgst: 0, sgst: 0 },
        12: { cgst: 0, sgst: 0 },
        18: { cgst: 0, sgst: 0 }
    };

    saleItems.forEach(item => {
        const gst = parseFloat(item.taxPercent) || 0;
        if (!(gst === 5 || gst === 12 || gst === 18)) return;

        // After-tax additional discount model:
        // - item.total is tax-inclusive MRP (after item-discount only)
        // - tax is extracted from item.total
        const baseAfterDisc = parseFloat(item.baseTotal) || 0;
        const includedTax = gst > 0 ? (baseAfterDisc * (gst / (100 + gst))) : 0;
        const halfTax = includedTax / 2;

        const cgstAmt = halfTax;
        const sgstAmt = halfTax;

        result[gst].cgst += cgstAmt;
        result[gst].sgst += sgstAmt;
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
    const stockQty = item.availableQty ?? item.availableQty;
    if (stockQty !== undefined && stockQty !== null) {
        const n = parseFloat(stockQty);
        return Number.isFinite(n) ? n : 0;
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
    renderSaleItems();
    recalculateBill();
    updateSaleItemsSummary();
}

function renderSaleItems() {
    const tbody = $('#saleItemsBody');
    tbody.empty();

    if (saleItems.length === 0) {
        $('#emptyCart').show();
        $('#saleItemsTable').hide();
        $('#clearCartBtn').hide();
        $('#saleItemsSummary').hide();
        $('#itemCount').text('0 items');
        return;
    }

    $('#emptyCart').hide();
    $('#saleItemsTable').show();
    $('#clearCartBtn').show();
    $('#saleItemsSummary').show();
    $('#itemCount').text(`${saleItems.length} item${saleItems.length > 1 ? 's' : ''}`);

    saleItems.forEach((item, index) => {
        const displayName = item.productName || item.medicineName || '';
        const displayUnit = item.uomName || item.unitType || 'PCS';
        const isStockItem = !!item.productId;
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
                    ${isStockItem ? `<span style="font-weight:700;color:var(--gray-700);">${displayUnit}</span>` : `
                    <select class="item-unit-select" onchange="updateItemUnitType(${index}, this.value)" id="unit-${index}">
                        <option value="Strip" ${item.unitType === 'Strip' ? 'selected' : ''}>Strip</option>
                        <option value="Tablet" ${item.unitType === 'Tablet' ? 'selected' : ''}>Tablet</option>
                    </select>`}
                </td>
                <td>
                    <input type="number" class="item-qty-input" value="${item.quantity}" min="1" max="${getMaxQtyForItem(item)}"
                           oninput="updateItemQuantity(${index}, this.value)" onchange="updateItemQuantity(${index}, this.value)" id="qty-${index}">
                </td>
                <td style="font-variant-numeric: tabular-nums;">${formatCurrency(item.price)}</td>
                <td>
                    <input type="number" class="item-discount-input" value="${item.discountPercent}" min="0" max="100" step="0.5"
                           oninput="updateItemDiscount(${index}, this.value)" onchange="updateItemDiscount(${index}, this.value)" id="disc-${index}">
                </td>
                <td style="color: var(--accent-600); font-variant-numeric: tabular-nums;">${formatCurrency(item.discountAmount)}</td>
                <td style="font-variant-numeric: tabular-nums;">${formatCurrency(item.taxAmount || 0)}</td>
                <td class="text-right" style="font-weight:700; font-variant-numeric: tabular-nums;">${formatCurrency(item.total)}</td>
                <td>
                    <button class="btn-remove" onclick="removeItem(${index})" title="Remove">
                        <i class="bi bi-trash3"></i>
                    </button>
                </td>
            </tr>
        `);
    });
}

function updateSaleItemsSummary() {
    const summary = $('#saleItemsSummary');
    if (!summary.length) return;

    if (!saleItems.length) {
        summary.hide();
        $('#saleSummaryItems').val('0');
        $('#saleSummaryMrp').val(formatCurrency(0));
        $('#saleSummaryDiscountAmt').val(formatCurrency(0));
        $('#saleSummaryTax').val(formatCurrency(0));
        $('#saleSummaryPayable').val(formatCurrency(0));
        return;
    }

    let subTotal = 0;
    let discountAmt = 0;
    let taxAmt = 0;
    let payable = 0;

    saleItems.forEach(item => {
        const price = parseFloat(item.price) || 0;
        const qty = parseFloat(item.quantity) || 0;
        const disc = parseFloat(item.discountAmount) || 0;
        const tax = parseFloat(item.taxAmount) || 0;
        const total = parseFloat(item.total);

        subTotal += price * qty;
        discountAmt += disc;
        taxAmt += tax;
        payable += Number.isFinite(total) ? total : (Math.max(0, (price * qty) - disc) + tax);
    });

    $('#saleSummaryItems').val(String(saleItems.length));
    $('#saleSummaryMrp').val(formatCurrency(subTotal));
    $('#saleSummaryDiscountAmt').val(formatCurrency(discountAmt));
    $('#saleSummaryTax').val(formatCurrency(taxAmt));
    $('#saleSummaryPayable').val(formatCurrency(payable));
    summary.show();
}

function updateItemQuantity(index, value) {
    const qty = parseInt(value) || 0;
    if (qty <= 0) {
        showToast('Quantity must be at least 1', 'error');
        $(`#qty-${index}`).val(saleItems[index].quantity);
        return;
    }
    const maxQty = getMaxQtyForItem(saleItems[index]);
    if (maxQty > 0 && qty > maxQty) {
        showToast(`Only ${maxQty} ${saleItems[index].unitType.toLowerCase()}(s) available`, 'error');
        $(`#qty-${index}`).val(saleItems[index].quantity);
        return;
    }
    saleItems[index].quantity = qty;
    recalculateItem(index);
    renderSaleItems();
    recalculateBill();

    // Keep additional discount fields aligned with subtotal changes
    syncAdditionalDiscountAmountFromPercent();
    updateSaleItemsSummary();
}

function updateItemDiscount(index, value) {
    const disc = parseFloat(value) || 0;
    if (disc < 0 || disc > 100) {
        showToast('Discount must be between 0 and 100%', 'error');
        $(`#disc-${index}`).val(saleItems[index].discountPercent);
        return;
    }
    saleItems[index].discountPercent = disc;
    recalculateItem(index);
    renderSaleItems();
    recalculateBill();

    syncAdditionalDiscountAmountFromPercent();
    updateSaleItemsSummary();
}

function removeItem(index) {
    const name = saleItems[index].productName || saleItems[index].medicineName;
    saleItems.splice(index, 1);
    renderSaleItems();
    // Cart changed => additional discount base changed; keep amount in sync before bill calc
    syncAdditionalDiscountAmountFromPercent();
    recalculateBill();
    updateSaleItemsSummary();
    updateCompleteSaleBtn();
    showToast(`Removed ${name}`, 'warning');
}

function clearAllItems() {
    saleItems = [];
    renderSaleItems();
    recalculateBill();
    $('#additionalDiscountPercent').val(0);
    $('#additionalDiscount').val(0);
    updateSaleItemsSummary();
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
    let subTotal = 0;
    let itemDiscount = 0;
    saleItems.forEach(item => {
        const price = parseFloat(item.price) || 0;
        const qty = parseFloat(item.quantity) || 0;
        const discAmt = parseFloat(item.discountAmount) || 0;
        subTotal += price * qty;
        itemDiscount += discAmt;
    });
    return Math.max(0, subTotal - itemDiscount);
}

function syncAdditionalDiscountAmountFromPercent() {
    const base = computeSubtotalBeforeAdditionalDiscount();
    const percent = getAdditionalDiscountPercent();
    const amt = base * percent / 100;
    $('#additionalDiscountPercent').val(percent);
    $('#additionalDiscount').val(amt.toFixed(2));
}

function recalculateBill() {
    let subTotal = 0;
    let itemDiscount = 0;
    let taxTotal = 0;
    let grandTotalBeforeAddDisc = 0;

    saleItems.forEach(item => {
        const price = parseFloat(item.price) || 0;
        const qty = parseFloat(item.quantity) || 0;
        const discAmt = parseFloat(item.discountAmount) || 0;

        subTotal += price * qty;
        itemDiscount += discAmt;
    });

    if (!isPrefillingEditSale) {
        syncAdditionalDiscountAmountFromPercent();
    }
    const additionalDiscount = getAdditionalDiscountAmount();

    // After-tax additional discount model:
    // - additional discount is applied only at summary level (payable), not distributed to line items
    saleItems.forEach(item => {
        const gst = parseFloat(item.taxPercent) || 0;
        const baseAfterDisc = parseFloat(item.baseTotal) || 0;

        const includedTax = gst > 0 ? (baseAfterDisc * (gst / (100 + gst))) : 0;
        item.taxAmount = includedTax;
        item.total = baseAfterDisc;

        taxTotal += includedTax;
        grandTotalBeforeAddDisc += baseAfterDisc;
    });

    const basePayable = Math.max(0, grandTotalBeforeAddDisc - additionalDiscount);
    const rounded = roundToNearestRupee(basePayable);
    const roundOff = rounded - basePayable;
    const displayGrandTotal = rounded;
    $('#billRoundOff').text(formatSignedCurrency(roundOff));

    $('#billSubtotal').text(formatCurrency(subTotal));
    $('#billTaxAmount').text(formatCurrency(taxTotal));
    $('#billItemDiscount').text('- ' + formatCurrency(itemDiscount));
    $('#billGrandTotal').text(formatCurrency(displayGrandTotal));

    updateTaxBreakupUI();

    // Keep summary in sync with bill calculations
    updateSaleItemsSummary();

    // Update payment amount
    updatePaymentAmount(displayGrandTotal);
}

$('#additionalDiscountPercent').on('input', debounce(function () {
    syncAdditionalDiscountAmountFromPercent();
    recalculateBill();
}, 200));

function updatePaymentAmount(grandTotal) {
    // Auto-fill payment amounts
    if (selectedPaymentMethod === 'Cash') {
        const cashReceived = parseFloat($('#cashAmount').val()) || 0;
        const change = cashReceived - grandTotal;
        $('#changeAmount').text(formatCurrency(Math.max(0, change)));
    } else if (selectedPaymentMethod === 'Card') {
        if (!isPrefillingEditSale) {
            $('#cardAmount').val(grandTotal.toFixed(2));
        }
    } else if (selectedPaymentMethod === 'UPI') {
        if (!isPrefillingEditSale) {
            $('#upiAmount').val(grandTotal.toFixed(2));
        }
    } else if (selectedPaymentMethod === 'Split') {
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
}

// ============ PAYMENT ============
function selectPaymentMethod(method, options) {
    const opts = options || {};
    selectedPaymentMethod = method;
    $('.payment-method-card').removeClass('selected');
    $(`#pm${method}`).addClass('selected');

    // Toggle forms
    $('.payment-detail-form').removeClass('show');
    $(`#payment${method}`).addClass('show');

    // Split cascade expects UPI to be dependent-only.
    $('#splitUpi').prop('readonly', method === 'Split');

    if (method === 'Split' && !opts.skipInitSplit) {
        initSplitPayments();
    }

    recalculateBill();
}

function getGrandTotal() {
    let subTotal = 0;
    let itemDiscount = 0;
    let grandTotalBeforeAddDisc = 0;
    saleItems.forEach(item => {
        const price = parseFloat(item.price) || 0;
        const qty = parseFloat(item.quantity) || 0;
        const discAmt = parseFloat(item.discountAmount) || 0;

        subTotal += price * qty;
        itemDiscount += discAmt;
    });
    // Percent-driven additional discount (source of truth is %)
    if (!isPrefillingEditSale) {
        syncAdditionalDiscountAmountFromPercent();
    }
    const additionalDiscount = getAdditionalDiscountAmount();

    // After-tax additional discount model: do not distribute discount into line totals
    saleItems.forEach(item => {
        const baseAfterDisc = parseFloat(item.baseTotal) || 0;
        grandTotalBeforeAddDisc += baseAfterDisc;
    });

    return roundToNearestRupee(Math.max(0, grandTotalBeforeAddDisc - additionalDiscount));
}

$('#cashAmount').on('input', function () {
    const received = parseFloat($(this).val()) || 0;
    const grandTotal = getGrandTotal();
    const change = received - grandTotal;
    $('#changeAmount').text(formatCurrency(Math.max(0, change)));
});

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
    if (selectedPaymentMethod !== 'Split') return;

    isSyncingSplit = true;

    const grandTotal = getGrandTotal();

    let cash = parseFloat($('#splitCash').val());
    let card = parseFloat($('#splitCard').val());
    let upi = parseFloat($('#splitUpi').val());
    if (Number.isNaN(cash)) cash = 0;
    if (Number.isNaN(card)) card = 0;
    if (Number.isNaN(upi)) upi = 0;

    cash = Math.max(0, cash);
    if (cash > grandTotal) cash = grandTotal;

    card = Math.max(0, card);
    if (card > grandTotal) card = grandTotal;

    upi = Math.max(0, upi);
    if (upi > grandTotal) upi = grandTotal;

    const remainingAfterCash = Math.max(0, grandTotal - cash);
    if (changedField === 'cash' || !changedField) {
        card = remainingAfterCash;
        upi = 0;
    } else {
        // card edited -> upi becomes remaining
        if (card > remainingAfterCash) card = remainingAfterCash;
        upi = Math.max(0, grandTotal - cash - card);
    }

    const remaining = Math.max(0, grandTotal - cash - card - upi);

    // Avoid fighting the user's typing: only normalize the field they are editing on blur/change.
    if (normalizeChangedField) {
        if (changedField === 'cash') $('#splitCash').val(cash.toFixed(2));
        if (changedField === 'card') $('#splitCard').val(card.toFixed(2));
    }

    // Always keep the dependent fields synced.
    if (changedField === 'cash' || !changedField) {
        $('#splitCard').val(card.toFixed(2));
    }

    $('#splitUpi').val(upi.toFixed(2));
    $('#splitRemaining').text(formatCurrency(remaining));

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

function completeSale() {
    if (saleItems.length === 0) {
        showToast('Please add at least one item', 'error');
        return;
    }

    const grandTotal = getGrandTotal();
    let payments = [];

    if (selectedPaymentMethod === 'Cash') {
        const cashReceived = parseFloat($('#cashAmount').val()) || 0;
        payments.push({ paymentMode: 'Cash', amount: grandTotal, reference: `Cash received: ${cashReceived}` });
    } else if (selectedPaymentMethod === 'Card') {
        const cardRefNo = ($('#cardRefNo').val() || '').trim();
        payments.push({ paymentMode: 'Card', amount: grandTotal, reference: cardRefNo || null });
    } else if (selectedPaymentMethod === 'UPI') {
        const upiRefNo = ($('#upiRefNo').val() || '').trim();
        payments.push({ paymentMode: 'UPI', amount: grandTotal, reference: upiRefNo || null });
    } else if (selectedPaymentMethod === 'Split') {
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

    const request = {
        saleId: editingSaleId || null,
        customerId: selectedCustomer?.id || null,
        customerName: selectedCustomer?.name || fallbackName || inferredSearchName,
        customerPhone: selectedCustomer?.phone || fallbackPhone || inferredSearchPhone,
        items: saleItems.map(i => ({
            productId: i.productId,
            productName: i.productName,
            batchNumber: i.batchNumber,
            expiryDate: i.expiryDate,
            uomName: i.uomName,
            unitPrice: i.price,
            quantity: i.quantity,
            unitType: i.unitType,
            discountPercent: i.discountPercent,
            taxPercent: parseFloat(i.taxPercent) || 0
        })),
        additionalDiscount: parseFloat($('#additionalDiscount').val()) || 0,
        payments: payments
    };

    // Disable button and show loading
    $('#completeSaleBtn').prop('disabled', true).html('<span class="spinner-pharmacy"></span> Processing...');
    document.getElementById('saleJson').value = JSON.stringify(request);
    document.getElementById('completeSaleForm').submit();
}

// Show server result after redirect
$(function () {
    const invoice = ($('#serverSaleSuccessInvoice').val() || '').toString();
    const saleIdRaw = ($('#serverSaleSuccessId').val() || '').toString();
    const saleId = parseInt(saleIdRaw, 10) || null;
    const err = ($('#serverSaleError').val() || '').toString();
    if (err) {
        showToast(err, 'error');
    }
    if (invoice) {
        $('#successInvoice').text(`#${invoice}`);
        $('#successOverlay').addClass('show');

        window.__lastCompletedSaleId = saleId;
        if (saleId && $('#printBillBtn').length) {
            $('#printBillBtn').show();
        }
    }
});

function printLastBill() {
    const saleId = window.__lastCompletedSaleId;
    if (!saleId) return;
    window.open(`${MVC_BASE}/Pdf?id=${encodeURIComponent(saleId)}`, '_blank');
}

function startNewSale() {
    selectedCustomer = null;
    saleItems = [];
    currentBatchInfo = null;
    selectedUnitType = 'PCS';
    selectedPaymentMethod = 'Cash';
    editingSaleId = null;
    window.__lastCompletedSaleId = null;

    // Reset UI
    $('#selectedCustomerCard').hide().empty();
    $('#customerSearch').val('').show();
    $('#toggleNewCustomerForm').show();
    $('#skipCustomerBtn').show();
    $('#newCustomerForm').removeClass('show');

    $('#batchSearch, #medicineSearch').val('');
    $('#batchInfoCard, #medicineBatchesCard').removeClass('show').empty();
    $('#advancedSearchResults').empty();
    $('#advBatchNumber, #advMedicineName, #advComposition, #advExpiryFrom, #advExpiryTo').val('');

    renderSaleItems();

    $('#additionalDiscount').val(0);
    recalculateBill();

    // Reset payment
    selectPaymentMethod('Cash');
    $('#cashAmount').val('');
    $('#changeAmount').text(formatCurrency(0));
    $('#cardRefNo, #upiRefNo').val('');
    $('#splitCardRefNo, #splitUpiRefNo').val('');
    $('#splitCash, #splitCard, #splitUpi').val('');

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
    if (editSaleId) {
        loadSaleForEdit(editSaleId);
    }
});

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

            // Reset current draft
            selectedCustomer = null;
            saleItems = [];
            currentBatchInfo = null;
            selectedUnitType = 'PCS';

            // Customer
            if (data.customer && data.customer.id) {
                selectedCustomer = { id: data.customer.id, name: data.customer.name || '', phone: data.customer.phone || '' };
                $('#selectedCustomerCard').html(`
                    <div class="customer-card" data-customer-json='${JSON.stringify(selectedCustomer).replace(/'/g, "&#39;")}'>
                        <div class="customer-avatar">
                            <i class="bi bi-person"></i>
                        </div>
                        <div class="customer-info">
                            <div class="customer-name">${selectedCustomer.name}</div>
                            <div class="customer-phone">${selectedCustomer.phone}</div>
                        </div>
                        <button class="customer-remove" onclick="removeCustomer()" title="Change">
                            <i class="bi bi-x-lg"></i>
                        </button>
                    </div>
                `).show();
                $('#customerSearch').hide();
                $('#toggleNewCustomerForm').hide();
                $('#skipCustomerBtn').hide();
            } else {
                $('#selectedCustomerCard').hide().empty();
                $('#customerSearch').show();
                $('#toggleNewCustomerForm').show();
                $('#skipCustomerBtn').show();
            }

            // Items
            const items = data.items || [];
            saleItems = [];
            items.forEach(i => {
                const stock = findStockByProductBatch(i.productId, i.batchNumber) || findStockByProductId(i.productId);
                const resolvedName = readStockProductName(stock) || '';
                const resolvedUom = readStockUom(stock) || 'PCS';
                const resolvedTaxPercent = detectGstPercentFromTaxName(readStockTaxName(stock));

                const qty = parseFloat(i.quantity) || 0;
                const price = parseFloat(i.price) || 0;

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
                    quantity: qty,
                    unitType: i.unitType || (i.uomName || 'PCS'),
                    price: price,
                    discountPercent: derivedDiscPercent,
                    discountAmount: 0,
                    taxPercent: effectiveTaxPercent,
                    taxAmount: 0,
                    baseTotal: 0,
                    total: 0,
                    availableQty: i.availableQty
                };
                saleItems.push(item);
                recalculateItem(saleItems.length - 1);
            });

            // Additional discount
            const addDisc = parseFloat(data.additionalDiscount) || 0;
            $('#additionalDiscount').val(addDisc.toFixed(2));
            // back-calc percent from current subtotal-after-item-discount
            const base = computeSubtotalBeforeAdditionalDiscount();
            const percent = base > 0 ? (addDisc / base) * 100 : 0;
            // Keep more precision to avoid amount drift after subsequent recalculations
            $('#additionalDiscountPercent').val(percent.toFixed(2));

            // Payment prefill
            const p = data.payment || {};
            const method = (p.method || 'Cash').toString();
            selectPaymentMethod(method, { skipInitSplit: method === 'Split' });

            // Cash
            if (p.cashReceived !== undefined && p.cashReceived !== null) {
                $('#cashAmount').val(parseFloat(p.cashReceived).toFixed(2));
            }
            // Card
            if (p.cardRefNo !== undefined && p.cardRefNo !== null) {
                $('#cardRefNo').val(p.cardRefNo);
            }
            if (p.cardAmount !== undefined && p.cardAmount !== null) {
                $('#cardAmount').val(parseFloat(p.cardAmount).toFixed(2));
            }
            // UPI
            if (p.upiRefNo !== undefined && p.upiRefNo !== null) {
                $('#upiRefNo').val(p.upiRefNo);
            }
            if (p.upiAmount !== undefined && p.upiAmount !== null) {
                $('#upiAmount').val(parseFloat(p.upiAmount).toFixed(2));
            }
            // Split
            if (p.splitCash !== undefined && p.splitCash !== null) {
                $('#splitCash').val(parseFloat(p.splitCash).toFixed(2));
            }
            if (p.splitCard !== undefined && p.splitCard !== null) {
                $('#splitCard').val(parseFloat(p.splitCard).toFixed(2));
            }
            if (p.splitUpi !== undefined && p.splitUpi !== null) {
                $('#splitUpi').val(parseFloat(p.splitUpi).toFixed(2));
            }
            if (p.splitCardRefNo !== undefined && p.splitCardRefNo !== null) {
                $('#splitCardRefNo').val(p.splitCardRefNo);
            }
            if (p.splitUpiRefNo !== undefined && p.splitUpiRefNo !== null) {
                $('#splitUpiRefNo').val(p.splitUpiRefNo);
            }
            if (method === 'Split') {
                const gt = getGrandTotal();
                const c = parseFloat($('#splitCash').val()) || 0;
                const ca = parseFloat($('#splitCard').val()) || 0;
                const u = parseFloat($('#splitUpi').val()) || 0;
                const rem = Math.max(0, gt - c - ca - u);
                $('#splitRemaining').text(formatCurrency(rem));
            }

            renderSaleItems();
            recalculateBill();
            updateCompleteSaleBtn();
            switchMedicineTab('batch');

            isPrefillingEditSale = false;
        })
        .catch(err => {
            isPrefillingEditSale = false;
            showToast(err?.message || 'Failed to load sale', 'error');
        });
}

