// =============================================
// PharmaCare POS - Sales Management JavaScript
// =============================================

// ============ STATE ============
let selectedCustomer = null;
let saleItems = [];
let selectedPaymentMethod = 'Cash';
let currentBatchInfo = null;
let debounceTimer = null;
let suppressBatchAutoSelectUntil = 0;
let isSyncingSplit = false;
let lastBatchQuickAdd = { key: null, at: 0 };
let inFlightBatchAdds = new Map();

const MVC_BASE = '/Sales';

const FIXED_TAX_PERCENT = 5;

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
                try { selectedCustomer = JSON.parse(json); } catch { selectedCustomer = null; }
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
                try { selectedCustomer = JSON.parse(json); } catch { selectedCustomer = null; }
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
    $('#medicineSearch').val('').focus();
}

// ============ BATCH SEARCH (TAB A) ============
function fetchBatchSearchResults(q, onDone) {
    fetch(`${MVC_BASE}/SearchBatch?q=${encodeURIComponent(q)}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(r => r.text())
        .then(html => {
            const dropdown = $('#batchDropdown');
            dropdown.html(html);
            dropdown.addClass('show');

            const items = [];
            dropdown.find('.autocomplete-item[data-product-id]').each(function () {
                const pid = $(this).attr('data-product-id');
                const bn = $(this).attr('data-batch-number');
                if (pid && bn !== undefined) items.push({ productId: pid, batchNumber: bn });
            });
            onDone(items);
        })
        .catch(() => {
            $('#batchDropdown').removeClass('show').empty();
            onDone([]);
        });
}

function renderBatchDropdown(data) {
    // HTML is rendered server-side via partial view
}

function addFromBatchSelection(productId, batchNumber) {
    suppressBatchAutoSelectUntil = Date.now() + 800;
    const key = `${productId}|${String(batchNumber || '').trim().toLowerCase()}`;
    const now = Date.now();
    if (lastBatchQuickAdd.key === key && (now - lastBatchQuickAdd.at) < 800) {
        return;
    }
    lastBatchQuickAdd = { key, at: now };

    addFromAdvancedSearch(productId, batchNumber);
    $('#batchDropdown').removeClass('show').empty();
    $('#batchInfoCard').removeClass('show').empty();
    $('#batchSearch').val('').focus();
}

function tryAutoSelectBatchFromSearch(q) {
    const query = (q || '').trim();
    if (query.length < 2) return;

    fetchBatchSearchResults(query, function (data) {
        if (!data || data.length === 0) {
            renderBatchDropdown([]);
            return;
        }

        const exact = data.find(b => (b.batchNumber || '').toLowerCase() === query.toLowerCase());
        if (exact) {
            $('#batchDropdown').removeClass('show').empty();
            addFromBatchSelection(exact.productId, exact.batchNumber);
            return;
        }

        // If the query is a prefix of a single batchNumber (common while typing/scanning), auto-select it
        const prefixMatches = data.filter(b => (b.batchNumber || '').toLowerCase().startsWith(query.toLowerCase()));
        if (prefixMatches.length === 1) {
            $('#batchDropdown').removeClass('show').empty();
            addFromBatchSelection(prefixMatches[0].productId, prefixMatches[0].batchNumber);
            return;
        }

        if (data.length === 1) {
            $('#batchDropdown').removeClass('show').empty();
            addFromBatchSelection(data[0].productId, data[0].batchNumber);
            return;
        }

        // Multiple results; show dropdown so user can pick
        renderBatchDropdown(data);
    });
}

$('#batchSearch').on('input', debounce(function () {
    const q = $(this).val().trim();
    if (q.length < 2) {
        $('#batchDropdown').removeClass('show').empty();
        return;
    }
    // If it looks like a full batch number, try to auto-select immediately (more reliable than relying on Enter)
    if (q.length >= 8) {
        tryAutoSelectBatchFromSearch(q);
        return;
    }

    fetchBatchSearchResults(q, function (data) {
        renderBatchDropdown(data);
    });
}));

// Allow typing/scanning a batch number and pressing Enter to immediately show details
$('#batchSearch').on('keydown', function (e) {
    if (e.key === 'Enter') {
        e.preventDefault();
        if (Date.now() < suppressBatchAutoSelectUntil) return;
        const q = $(this).val().trim();
        tryAutoSelectBatchFromSearch(q);
    }
});

// Some barcode scanners trigger keypress (or only reliably populate value before keypress)
$('#batchSearch').on('keypress', function (e) {
    if (e.key === 'Enter') {
        e.preventDefault();
        if (Date.now() < suppressBatchAutoSelectUntil) return;
        const q = $(this).val().trim();
        tryAutoSelectBatchFromSearch(q);
    }
});

// Fallback: if user/scanner fills the input and focus moves away, still try to load details
$('#batchSearch').on('change blur', function () {
    const q = $(this).val().trim();
    if (q.length >= 2) {
        if (Date.now() < suppressBatchAutoSelectUntil) return;
        tryAutoSelectBatchFromSearch(q);
    }
});

function selectStock(productId, batchNumber, source) {
    fetch(`${MVC_BASE}/GetStockByProductBatch?productId=${encodeURIComponent(productId)}&batchNumber=${encodeURIComponent(batchNumber)}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(r => {
            if (!r.ok) throw new Error('not found');
            return r.text();
        })
        .then(html => {
            const temp = document.createElement('div');
            temp.innerHTML = html;
            const payload = temp.querySelector('.stock-details-payload');
            const json = payload?.getAttribute('data-stock-json');
            if (!json) throw new Error('payload missing');
            const b = normalizeStockPayload(JSON.parse(json));
            if (!b) throw new Error('payload invalid');

            currentBatchInfo = b;

            const expiryStr = formatExpiryDate(b.expiryDate) || '-';
            const expDate = b.expiryDate ? new Date(b.expiryDate) : null;
            const today = new Date();
            today.setHours(0, 0, 0, 0);
            const isExpired = !!(expDate && !Number.isNaN(expDate.getTime()) && expDate < today);
            const nearLimit = new Date(today);
            nearLimit.setDate(nearLimit.getDate() + 90);
            const isNearExpiry = !!(expDate && !Number.isNaN(expDate.getTime()) && !isExpired && expDate <= nearLimit);
            const expiryClass = isExpired ? 'danger' : (isNearExpiry ? 'warning' : '');
            const expiryWarning = isExpired ? '<span style="color:var(--danger-500);font-weight:600;font-size:0.78rem;">⚠ EXPIRED</span>' :
                (isNearExpiry ? '<span style="color:var(--warning-500);font-weight:600;font-size:0.78rem;">⚠ Near Expiry</span>' : '');

            const fixedUnit = readStockUom(b);
            selectedUnitType = fixedUnit;

            const availableQty = readStockQty(b);
            const productName = readStockProductName(b);
            const batchNo = readStockBatchNumber(b);

            currentBatchInfo.availableQty = availableQty;
            currentBatchInfo.uomName = fixedUnit;
            currentBatchInfo.productName = productName;
            currentBatchInfo.batchNumber = batchNo;

            const targetCard = source === 'batch' ? '#batchInfoCard' : '#medicineBatchesCard';
            $(targetCard).html(`
                <div class="batch-info-grid">
                    <div class="batch-info-item">
                        <div class="batch-info-label">Product</div>
                        <div class="batch-info-value">${productName}</div>
                    </div>
                    <div class="batch-info-item">
                        <div class="batch-info-label">Batch Number</div>
                        <div class="batch-info-value">${batchNo}</div>
                    </div>
                    <div class="batch-info-item">
                        <div class="batch-info-label">Expiry Date</div>
                        <div class="batch-info-value ${expiryClass}">${expiryStr} ${expiryWarning}</div>
                    </div>
                    <div class="batch-info-item">
                        <div class="batch-info-label">Available</div>
                        <div class="batch-info-value">${availableQty} ${fixedUnit}</div>
                    </div>
                    <div class="batch-info-item">
                        <div class="batch-info-label">Unit Price</div>
                        <div class="batch-info-value">${formatCurrency(readStockMrp(b))}</div>
                    </div>
                    <div class="batch-info-item">
                        <div class="batch-info-label">Manufacturer</div>
                        <div class="batch-info-value">${b.manufacturer || 'N/A'}</div>
                    </div>
                </div>
                <div class="batch-actions">
                    <div>
                        <label class="form-label-custom">Unit Type</label>
                        <div class="unit-toggle" id="unitToggle">
                            <button class="active" disabled>${fixedUnit}</button>
                        </div>
                    </div>
                    <div style="flex:1;">
                        <label class="form-label-custom">Quantity</label>
                        <input type="number" class="pharmacy-input" id="addQty" value="1" min="1" max="${availableQty}" style="width:100px;">
                    </div>
                    <div>
                        <label class="form-label-custom">&nbsp;</label>
                        <button class="btn-outline-pharmacy" onclick="cancelBatchSelection('${source}')" style="margin-right:8px;">
                            <i class="bi bi-x-circle"></i> Cancel
                        </button>
                        <button class="btn-primary-pharmacy" onclick="addToCart()">
                            <i class="bi bi-cart-plus"></i> Add
                        </button>
                    </div>
                </div>
            `).addClass('show');

            const el = document.querySelector(targetCard);
            if (el) {
                el.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            }

            $('#batchDropdown, #medicineDropdown').removeClass('show');
        })
        .catch(() => {
            showToast('Failed to load stock', 'error');
        });
}

function cancelBatchSelection(source) {
    suppressBatchAutoSelectUntil = Date.now() + 800;
    currentBatchInfo = null;
    selectedUnitType = 'PCS';

    if (source === 'batch') {
        $('#batchInfoCard').removeClass('show').empty();
        $('#batchDropdown').removeClass('show').empty();
        $('#batchSearch').val('').focus();
    } else {
        $('#medicineBatchesCard').removeClass('show').empty();
        $('#medicineDropdown').removeClass('show').empty();
        $('#medicineSearch').focus();
    }
}

function getSubTotal() {
    let subTotal = 0;
    saleItems.forEach(item => {
        subTotal += item.price * item.quantity;
    });
    return subTotal;
}

function getTaxTotal() {
    let taxTotal = 0;
    saleItems.forEach(item => {
        taxTotal += item.taxAmount || 0;
    });
    return taxTotal;
}

function updateSaleItemsSummary() {
    if (!saleItems.length) {
        $('#saleItemsSummary').hide();
        return;
    }
    const subTotal = getSubTotal();

    let itemDiscount = 0;
    saleItems.forEach(item => {
        itemDiscount += item.discountAmount || 0;
    });
    const taxTotal = getTaxTotal();
    const payable = Math.max(0, subTotal - itemDiscount + taxTotal);

    $('#saleSummaryItems').val(`${saleItems.length}`);
    $('#saleSummaryMrp').val(formatCurrency(subTotal));
    $('#saleSummaryDiscountAmt').val(formatCurrency(itemDiscount));
    $('#saleSummaryTax').val(formatCurrency(taxTotal));
    $('#saleSummaryPayable').val(formatCurrency(payable));

    $('#saleItemsSummary').show();
}

let isSyncingAdditionalDiscount = false;

function getAdditionalDiscountBaseAmount() {
    const subTotal = getSubTotal();
    let itemDiscount = 0;
    saleItems.forEach(item => {
        itemDiscount += item.discountAmount || 0;
    });
    return Math.max(0, subTotal - itemDiscount);
}

function getAdditionalDiscountAmount() {
    const base = getAdditionalDiscountBaseAmount();
    let percent = parseFloat($('#additionalDiscountPercent').val()) || 0;
    percent = Math.min(100, Math.max(0, percent));
    return base * percent / 100;
}

function syncAdditionalDiscountAmountFromPercent() {
    if (isSyncingAdditionalDiscount) return;
    isSyncingAdditionalDiscount = true;
    try {
        const base = getAdditionalDiscountBaseAmount();
        let percent = parseFloat($('#additionalDiscountPercent').val()) || 0;
        percent = Math.min(100, Math.max(0, percent));
        const amount = base * percent / 100;
        $('#additionalDiscountPercent').val(percent);
        $('#additionalDiscount').val(amount.toFixed(2));
    } finally {
        isSyncingAdditionalDiscount = false;
    }
}

$('#additionalDiscountPercent').on('input change', function () {
    syncAdditionalDiscountAmountFromPercent();
    recalculateBill();
    updateSaleItemsSummary();
});

let selectedUnitType = 'PCS';

function setUnitType(type) {
    selectedUnitType = type;
    $('#unitToggle button').removeClass('active');
    $(`#unitToggle button:contains('${type}')`).addClass('active');
    if (currentBatchInfo) {
        const max = readStockQty(currentBatchInfo);
        $('#addQty').attr('max', max);
    }
}

// ============ MEDICINE SEARCH (TAB B) ============
$('#medicineSearch').on('input', debounce(function () {
    const q = $(this).val().trim();
    if (q.length < 2) {
        $('#medicineDropdown').removeClass('show').empty();
        return;
    }
    fetch(`${MVC_BASE}/SearchMedicine?q=${encodeURIComponent(q)}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(r => r.text())
        .then(html => {
            const dropdown = $('#medicineDropdown');
            dropdown.html(html);
            dropdown.addClass('show');
        })
        .catch(() => {
            $('#medicineDropdown').removeClass('show').empty();
        });
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

    fetch(`${MVC_BASE}/GetStockByProductBatch?productId=${encodeURIComponent(productId)}&batchNumber=${encodeURIComponent(batchNumber)}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(r => {
            if (!r.ok) throw new Error('not found');
            return r.text();
        })
        .then(html => {
            const temp = document.createElement('div');
            temp.innerHTML = html;
            const payload = temp.querySelector('.stock-details-payload');
            const json = payload?.getAttribute('data-stock-json');
            if (!json) throw new Error('payload missing');
            const b = normalizeStockPayload(JSON.parse(json));
            if (!b) throw new Error('payload invalid');

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
        })
        .catch(() => {
            showToast('Failed to add item', 'error');
        })
        .finally(function () {
            // Allow re-add after request finishes; quick-add is still protected by timestamp guard
            inFlightBatchAdds.delete(addKey);
        });
}

function recalculateItem(index) {
    const item = saleItems[index];
    const price = parseFloat(item.price) || 0;
    const qty = parseFloat(item.quantity) || 0;
    const discPercent = parseFloat(item.discountPercent) || 0;
    const taxPercent = parseFloat(item.taxPercent) || 0;

    const lineTotal = price * qty;
    item.discountAmount = lineTotal * discPercent / 100;
    const taxable = Math.max(0, lineTotal - (parseFloat(item.discountAmount) || 0));
    item.taxPercent = taxPercent;
    item.taxAmount = taxable * taxPercent / 100;
    item.total = taxable + (parseFloat(item.taxAmount) || 0);
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

        const price = parseFloat(item.price) || 0;
        const qty = parseFloat(item.quantity) || 0;
        const lineTotal = price * qty;
        const discAmt = parseFloat(item.discountAmount) || 0;
        const taxable = Math.max(0, lineTotal - discAmt);

        const half = gst / 2;
        const cgstAmt = taxable * half / 100;
        const sgstAmt = taxable * half / 100;

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

function recalculateBill() {
    let subTotal = 0;
    let itemDiscount = 0;
    let taxTotal = 0;

    saleItems.forEach(item => {
        const price = parseFloat(item.price) || 0;
        const qty = parseFloat(item.quantity) || 0;
        const discAmt = parseFloat(item.discountAmount) || 0;
        const taxAmt = parseFloat(item.taxAmount) || 0;

        subTotal += price * qty;
        itemDiscount += discAmt;
        taxTotal += taxAmt;
    });

    syncAdditionalDiscountAmountFromPercent();
    const additionalDiscount = getAdditionalDiscountAmount();
    const unroundedGrandTotal = subTotal - itemDiscount - additionalDiscount + taxTotal;

    const baseGrandTotal = Math.max(0, unroundedGrandTotal);

    let displayGrandTotal = baseGrandTotal;
    if (selectedPaymentMethod === 'Cash') {
        const rounded = roundToNearestRupee(baseGrandTotal);
        const roundOff = rounded - baseGrandTotal;
        displayGrandTotal = rounded;
        $('#billRoundOff').text(formatSignedCurrency(roundOff));
    } else {
        $('#billRoundOff').text(formatCurrency(0));
    }

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

function updatePaymentAmount(grandTotal) {
    // Auto-fill payment amounts
    if (selectedPaymentMethod === 'Cash') {
        const cashReceived = parseFloat($('#cashAmount').val()) || 0;
        const change = cashReceived - grandTotal;
        $('#changeAmount').text(formatCurrency(Math.max(0, change)));
    } else if (selectedPaymentMethod === 'Card') {
        $('#cardAmount').val(grandTotal.toFixed(2));
    } else if (selectedPaymentMethod === 'UPI') {
        $('#upiAmount').val(grandTotal.toFixed(2));
    } else if (selectedPaymentMethod === 'Split') {
        syncSplitPayments();
    }
}

// ============ PAYMENT ============
function selectPaymentMethod(method) {
    selectedPaymentMethod = method;
    $('.payment-method-card').removeClass('selected');
    $(`#pm${method}`).addClass('selected');

    // Toggle forms
    $('.payment-detail-form').removeClass('show');
    $(`#payment${method}`).addClass('show');

    if (method === 'Split') {
        initSplitPayments();
    }

    recalculateBill();
}

function getGrandTotal() {
    let subTotal = 0;
    let itemDiscount = 0;
    let taxTotal = 0;
    saleItems.forEach(item => {
        const price = parseFloat(item.price) || 0;
        const qty = parseFloat(item.quantity) || 0;
        const discAmt = parseFloat(item.discountAmount) || 0;
        const taxAmt = parseFloat(item.taxAmount) || 0;

        subTotal += price * qty;
        itemDiscount += discAmt;
        taxTotal += taxAmt;
    });
    // Percent-driven additional discount (source of truth is %)
    syncAdditionalDiscountAmountFromPercent();
    const additionalDiscount = getAdditionalDiscountAmount();
    const unroundedGrandTotal = subTotal - itemDiscount - additionalDiscount + taxTotal;
    const baseGrandTotal = Math.max(0, unroundedGrandTotal);
    if (selectedPaymentMethod === 'Cash') {
        return roundToNearestRupee(baseGrandTotal);
    }
    return baseGrandTotal;
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
    if (Number.isNaN(cash)) cash = 0;
    if (Number.isNaN(card)) card = 0;

    cash = Math.max(0, cash);
    if (cash > grandTotal) cash = grandTotal;

    const remainingAfterCash = Math.max(0, grandTotal - cash);
    if (changedField === 'cash' || !changedField) {
        card = remainingAfterCash;
    } else {
        card = Math.max(0, card);
        if (card > remainingAfterCash) card = remainingAfterCash;
    }

    const upi = Math.max(0, grandTotal - cash - card);

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
    $('#splitRemaining').text(formatCurrency(0));

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
        const last4 = $('#cardLast4').val().trim();
        const cardType = $('#cardType').val();
        payments.push({ paymentMode: 'Card', amount: grandTotal, reference: `${cardType} - ${last4}` });
    } else if (selectedPaymentMethod === 'UPI') {
        const upiId = $('#upiId').val().trim();
        payments.push({ paymentMode: 'UPI', amount: grandTotal, reference: upiId });
    } else if (selectedPaymentMethod === 'Split') {
        const cash = parseFloat($('#splitCash').val()) || 0;
        const card = parseFloat($('#splitCard').val()) || 0;
        const upi = parseFloat($('#splitUpi').val()) || 0;
        if (Math.abs((cash + card + upi) - grandTotal) > 0.01) {
            showToast('Split payment total does not match grand total', 'error');
            return;
        }
        const cardType = $('#cardType').val();
        const cardRef = $('#cardLast4').val().trim();
        const upiRef = $('#upiId').val().trim();

        const cardReference = cardRef ? `${cardType} - ${cardRef}` : (cardType || null);
        const upiReference = upiRef || null;

        if (cash > 0) payments.push({ paymentMode: 'Cash', amount: parseFloat(cash.toFixed(2)), reference: null });
        if (card > 0) payments.push({ paymentMode: 'Card', amount: parseFloat(card.toFixed(2)), reference: cardReference });
        if (upi > 0) payments.push({ paymentMode: 'UPI', amount: parseFloat(upi.toFixed(2)), reference: upiReference });
    }

    const request = {
        customerId: selectedCustomer?.id || null,
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
            taxPercent: 5
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
    const err = ($('#serverSaleError').val() || '').toString();
    if (err) {
        showToast(err, 'error');
    }
    if (invoice) {
        $('#successInvoice').text(`#${invoice}`);
        $('#successOverlay').addClass('show');
    }
});

function startNewSale() {
    selectedCustomer = null;
    saleItems = [];
    currentBatchInfo = null;
    selectedUnitType = 'PCS';
    selectedPaymentMethod = 'Cash';

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
    $('#cardLast4, #upiId').val('');
    $('#splitCash, #splitCard, #splitUpi').val('');

    $('#completeSaleBtn').prop('disabled', true).html('<i class="bi bi-check-circle"></i> Complete Sale');
    $('#successOverlay').removeClass('show');

    updateCompleteSaleBtn();
    switchMedicineTab('batch');
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
    updateCompleteSaleBtn();
    recalculateBill();
});

