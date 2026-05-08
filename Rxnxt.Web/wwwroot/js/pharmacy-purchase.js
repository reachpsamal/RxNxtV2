let purchaseItems = [];
let selectedSupplier = null;

let purchaseEntryProduct = null;
let purchaseEntryBatchStock = null;

let supplierSuggestions = [];
let supplierActiveIndex = -1;

let purchaseProductSuggestions = [];
let purchaseProductActiveIndex = -1;

let purchaseBatchSuggestions = [];
let purchaseBatchActiveIndex = -1;

function parseGstPercentFromTaxName(taxName) {
    const s = (taxName || '').toString();
    const m = s.match(/(\d+(?:\.\d+)?)\s*%/);
    if (!m) return 0;
    const v = parseFloat(m[1]);
    return Number.isFinite(v) ? v : 0;
}

function mapUomNameToUnit(uomName) {
    const s = (uomName || '').toString().trim().toLowerCase();
    if (!s) return null;
    if (s.includes('pack')) return 'Pack';
    if (s.includes('strip')) return 'Strip';
    if (s.includes('tab')) return 'Tablet';
    return null;
}

function showToast(message, type = 'info') {
    const container = document.getElementById('toastContainer');
    if (!container) return;

    const icons = { success: 'bi-check-circle-fill', error: 'bi-x-circle-fill', warning: 'bi-exclamation-triangle-fill', info: 'bi-info-circle-fill' };
    const t = (type === 'danger') ? 'error' : type;

    const toast = document.createElement('div');
    toast.className = `toast-item ${t}`;
    toast.innerHTML = `<i class="bi ${icons[t] || icons.info}"></i> ${message}`;
    container.appendChild(toast);
    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transform = 'translateX(30px)';
        toast.style.transition = 'all 0.3s ease';
        setTimeout(() => toast.remove(), 300);
    }, 3500);
}

function formatCurrency(amount) {
    return '₹ ' + parseFloat(amount || 0).toFixed(2);
}

function formatAmount(amount) {
    return parseFloat(amount || 0).toFixed(2);
}

function debounce(fn, delay) {
    let t;
    return function (...args) {
        clearTimeout(t);
        t = setTimeout(() => fn.apply(this, args), delay);
    };
}

function normalizeText(s) {
    return (s || '').toString().trim().toLowerCase();
}

function getStocksPrefetch() {
    return Array.isArray(window.__prefetchStocks) ? window.__prefetchStocks : [];
}

function setTodayDates() {
    const today = new Date();
    const yyyy = today.getFullYear();
    const mm = String(today.getMonth() + 1).padStart(2, '0');
    const dd = String(today.getDate()).padStart(2, '0');
    const s = `${yyyy}-${mm}-${dd}`;
    $('#purchaseRefDate').val(s);
}

function editPurchaseItem(index, key, value) {
    const it = purchaseItems[index];
    if (!it) return;
    it[key] = value;
}

function commitPurchaseItem(index) {
    const it = purchaseItems[index];
    if (!it) return;

    const line = calcLine(it);
    it.taxable = line.afterDisc;
    it.discountAmount = line.discAmt;
    it.taxAmount = line.taxAmt;
    it.lineTotal = line.total;

    renderPurchaseItems();
}

function handlePurchaseItemEditKeydown(e, index) {
    if (e.key !== 'Enter') return;
    e.preventDefault();
    commitPurchaseItem(index);
}

function renderPurchaseItems() {
    const hasItems = purchaseItems.length > 0;
    $('#purchaseItemsTable').toggle(hasItems);
    $('#emptyPurchaseCart').toggle(!hasItems);
    $('#clearPurchaseBtn').toggle(hasItems);

    const $body = $('#purchaseItemsBody');
    $body.empty();

    purchaseItems.forEach((it, idx) => {
        const row = `
            <tr>
                <td>${idx + 1}</td>
                <td>
                    <div style="font-weight:700; color: var(--gray-800);">${it.productName}</div>
                    <div style="font-size:0.78rem; color: var(--gray-500);">${it.manufacturer || ''}</div>
                </td>
                <td><input type="text" class="pharmacy-input" value="${it.batchNumber || ''}" oninput="updatePurchaseItem(${idx}, 'batchNumber', this.value)" /></td>
                <td><input type="date" class="pharmacy-input" value="${it.expiryDate || ''}" oninput="updatePurchaseItem(${idx}, 'expiryDate', this.value)" /></td>
                <td>
                    <select class="pharmacy-input" onchange="updatePurchaseItem(${idx}, 'unit', this.value)">
                        <option value="Pack" ${it.unit === 'Pack' ? 'selected' : ''}>Pack</option>
                        <option value="Strip" ${it.unit === 'Strip' ? 'selected' : ''}>Strip</option>
                        <option value="Tablet" ${it.unit === 'Tablet' ? 'selected' : ''}>Tablet</option>
                    </select>
                </td>
                <td><input type="number" class="pharmacy-input" value="${it.qty}" min="0" step="0.01" oninput="editPurchaseItem(${idx}, 'qty', this.value)" onblur="commitPurchaseItem(${idx})" onkeydown="handlePurchaseItemEditKeydown(event, ${idx})" /></td>
                <td><input type="number" class="pharmacy-input" value="${it.purchaseRate}" min="0" step="0.01" oninput="editPurchaseItem(${idx}, 'purchaseRate', this.value)" onblur="commitPurchaseItem(${idx})" onkeydown="handlePurchaseItemEditKeydown(event, ${idx})" /></td>
                <td><input type="number" class="pharmacy-input" value="${it.mrp}" min="0" step="0.01" oninput="editPurchaseItem(${idx}, 'mrp', this.value)" onblur="commitPurchaseItem(${idx})" onkeydown="handlePurchaseItemEditKeydown(event, ${idx})" /></td>
                <td><input type="number" class="pharmacy-input" value="${it.discountPercent}" min="0" step="0.01" oninput="editPurchaseItem(${idx}, 'discountPercent', this.value)" onblur="commitPurchaseItem(${idx})" onkeydown="handlePurchaseItemEditKeydown(event, ${idx})" /></td>
                <td class="text-right" style="font-variant-numeric: tabular-nums;">${formatAmount(it.discountAmount)}</td>
                <td><input type="number" class="pharmacy-input" value="${it.gstPercent}" min="0" step="0.01" oninput="editPurchaseItem(${idx}, 'gstPercent', this.value)" onblur="commitPurchaseItem(${idx})" onkeydown="handlePurchaseItemEditKeydown(event, ${idx})" /></td>
                <td class="text-right" style="font-variant-numeric: tabular-nums;">${formatAmount(it.lineTotal)}</td>
                <td><button class="btn-ghost" type="button" onclick="removePurchaseItem(${idx})" title="Remove"><i class="bi bi-x-lg"></i></button></td>
            </tr>`;
        $body.append(row);
    });

    recalcPurchaseSummary();
}

function calcLine(it) {
    const qty = parseFloat(it.qty) || 0;
    const rate = parseFloat(it.purchaseRate) || 0;
    const discPct = Math.max(0, parseFloat(it.discountPercent) || 0);
    const gstPct = Math.max(0, parseFloat(it.gstPercent) || 0);

    const taxable = qty * rate;
    const discAmt = taxable * (discPct / 100);
    const afterDisc = Math.max(0, taxable - discAmt);
    const taxAmt = afterDisc * (gstPct / 100);
    const total = afterDisc + taxAmt;

    return { taxable, discAmt, afterDisc, taxAmt, total };
}

function updatePurchaseItem(index, key, value) {
    const it = purchaseItems[index];
    if (!it) return;

    if (key === 'qty' || key === 'purchaseRate' || key === 'mrp' || key === 'discountPercent' || key === 'gstPercent') {
        it[key] = value;
        const line = calcLine(it);
        it.taxable = line.afterDisc;
        it.discountAmount = line.discAmt;
        it.taxAmount = line.taxAmt;
        it.lineTotal = line.total;
    } else {
        it[key] = value;
    }

    renderPurchaseItems();
}

function removePurchaseItem(index) {
    purchaseItems.splice(index, 1);
    renderPurchaseItems();
}

function clearAllPurchaseItems() {
    purchaseItems = [];
    renderPurchaseItems();
}

function addPurchaseItemFromStock(stock) {
    purchaseEntryProduct = stock;
    purchaseEntryBatchStock = null;

    $('#purchaseBatchSearch').prop('disabled', false).val('');
    $('#purchaseUnit').prop('disabled', false);
    $('#purchaseEntryQty').prop('disabled', false).val('');
    $('#purchaseEntryRate').prop('disabled', false).val('');
    $('#purchaseEntryMrp').prop('disabled', false).val('');
    $('#purchaseEntryDiscPct').prop('disabled', false).val('0');
    $('#purchaseEntryGstPct').prop('disabled', false).val('0');
    $('#purchaseAddItemBtn').prop('disabled', false);

    $('#purchaseBatchSearch').focus();
    $('#purchaseProductSearch').val(stock && stock.productName ? stock.productName : '');
    $('#purchaseProductDropdown').hide().empty();
}

function renderProductDropdown(items) {
    const $dd = $('#purchaseProductDropdown');
    if (!items.length) {
        $dd.hide().empty();
        purchaseProductSuggestions = [];
        purchaseProductActiveIndex = -1;
        return;
    }

    purchaseProductSuggestions = items;
    if (purchaseProductActiveIndex >= items.length) purchaseProductActiveIndex = items.length - 1;

    const html = items.map((s, idx) => `
        <div class="autocomplete-item ${idx === purchaseProductActiveIndex ? 'active' : ''}" onclick='selectProduct(${JSON.stringify(s).replace(/'/g, "\\'")})'>
            <div class="item-main">${s.productName}</div>
        </div>`).join('');

    $dd.html(html).show();
}

window.selectProduct = function (stock) {
    addPurchaseItemFromStock(stock);
};

function setPurchaseProductActiveIndex(nextIndex) {
    const $dd = $('#purchaseProductDropdown');
    const items = $dd.find('.autocomplete-item');
    if (!items.length) {
        purchaseProductActiveIndex = -1;
        return;
    }

    const count = items.length;
    let idx = nextIndex;
    if (idx < 0) idx = count - 1;
    if (idx >= count) idx = 0;

    purchaseProductActiveIndex = idx;
    items.removeClass('active');
    const $active = $(items.get(idx));
    $active.addClass('active');

    const ddEl = $dd.get(0);
    const activeEl = $active.get(0);
    if (ddEl && activeEl) {
        const ddRect = ddEl.getBoundingClientRect();
        const elRect = activeEl.getBoundingClientRect();
        if (elRect.top < ddRect.top) {
            ddEl.scrollTop -= (ddRect.top - elRect.top);
        } else if (elRect.bottom > ddRect.bottom) {
            ddEl.scrollTop += (elRect.bottom - ddRect.bottom);
        }
    }
}

function clearPurchaseEntry() {
    purchaseEntryProduct = null;
    purchaseEntryBatchStock = null;
    $('#purchaseProductSearch').val('');
    $('#purchaseProductDropdown').hide().empty();
    $('#purchaseBatchSearch').prop('disabled', true).val('');
    $('#purchaseBatchDropdown').hide().empty();
    $('#purchaseUnit').prop('disabled', true).val('Strip');
    $('#purchaseEntryQty').prop('disabled', true).val('');
    $('#purchaseEntryRate').prop('disabled', true).val('');
    $('#purchaseEntryMrp').prop('disabled', true).val('');
    $('#purchaseEntryDiscPct').prop('disabled', true).val('0');
    $('#purchaseEntryGstPct').prop('disabled', true).val('0');
    $('#purchaseAddItemBtn').prop('disabled', true);
}

function renderBatchDropdown(items, typedValue) {
    const $dd = $('#purchaseBatchDropdown');
    const q = (typedValue || '').trim();
    if (!items.length && !q) {
        $dd.hide().empty();
        purchaseBatchSuggestions = [];
        purchaseBatchActiveIndex = -1;
        return;
    }

    const createOption = q ? {
        __create: true,
        batchNumber: q
    } : null;

    purchaseBatchSuggestions = [
        ...(createOption ? [createOption] : []),
        ...items
    ];

    if (purchaseBatchSuggestions.length && purchaseBatchActiveIndex >= purchaseBatchSuggestions.length) {
        purchaseBatchActiveIndex = purchaseBatchSuggestions.length - 1;
    }

    const html = [
        ...(createOption ? [`
            <div class="autocomplete-item ${purchaseBatchActiveIndex === 0 ? 'active' : ''}" onclick='selectPurchaseBatch(${JSON.stringify(createOption).replace(/'/g, "\\'")})'>
                <div class="item-main">Create new batch: ${q}</div>
                <div class="item-sub">New batch entry</div>
            </div>`] : []),
        ...items.map((b, idx) => {
            const actualIndex = (createOption ? 1 : 0) + idx;
            return `
            <div class="autocomplete-item ${purchaseBatchActiveIndex === actualIndex ? 'active' : ''}" onclick='selectPurchaseBatch(${JSON.stringify(b).replace(/'/g, "\\'")})'>
                <div class="item-main">${b.batchNumber || '-'}</div>
                <div class="item-sub">Exp: ${b.expiryDate ? b.expiryDate.substring(0,10) : '-'} | Avl: ${b.availableQty != null ? b.availableQty : 0}</div>
            </div>`;
        })
    ].join('');

    $dd.html(html).show();
}

function setPurchaseBatchActiveIndex(nextIndex) {
    const $dd = $('#purchaseBatchDropdown');
    const items = $dd.find('.autocomplete-item');
    if (!items.length) {
        purchaseBatchActiveIndex = -1;
        return;
    }

    const count = items.length;
    let idx = nextIndex;
    if (idx < 0) idx = count - 1;
    if (idx >= count) idx = 0;

    purchaseBatchActiveIndex = idx;
    items.removeClass('active');
    const $active = $(items.get(idx));
    $active.addClass('active');

    const ddEl = $dd.get(0);
    const activeEl = $active.get(0);
    if (ddEl && activeEl) {
        const ddRect = ddEl.getBoundingClientRect();
        const elRect = activeEl.getBoundingClientRect();
        if (elRect.top < ddRect.top) {
            ddEl.scrollTop -= (ddRect.top - elRect.top);
        } else if (elRect.bottom > ddRect.bottom) {
            ddEl.scrollTop += (elRect.bottom - ddRect.bottom);
        }
    }
}

window.selectPurchaseBatch = async function (b) {
    if (!purchaseEntryProduct) return;

    if (b && b.__create) {
        purchaseEntryBatchStock = null;
        $('#purchaseBatchSearch').val(b.batchNumber || '');
        $('#purchaseBatchDropdown').hide().empty();
        $('#purchaseEntryQty').focus();
        return;
    }

    purchaseEntryBatchStock = b ? {
        ...b,
        expiryDate: b.expiryDate ? String(b.expiryDate) : null
    } : b;
    $('#purchaseBatchSearch').val(b.batchNumber || '');
    $('#purchaseBatchDropdown').hide().empty();

    try {
        const res = await fetch(`/api/api/stocks/by-product-batch?productId=${encodeURIComponent(purchaseEntryProduct.productId)}&batchNumber=${encodeURIComponent(b.batchNumber || '')}`);
        if (res.ok) {
            const data = await res.json().catch(() => null);
            if (data) {
                const unit = mapUomNameToUnit(data.uomName);
                if (unit) $('#purchaseUnit').val(unit);

                const gstPct = parseGstPercentFromTaxName(data.taxName);
                $('#purchaseEntryGstPct').val(gstPct);
            }
        }
    } catch {
        // ignore auto-fill errors
    }

    $('#purchaseEntryQty').focus();
};

$('#purchaseBatchSearch').on('input', debounce(async function () {
    if (!purchaseEntryProduct) return;
    const typed = ($(this).val() || '').trim();

    try {
        const url = `/api/api/product-stocks/batches?productId=${encodeURIComponent(purchaseEntryProduct.productId)}&q=${encodeURIComponent(typed)}`;
        const res = await fetch(url);
        if (!res.ok) {
            renderBatchDropdown([], typed);
            return;
        }

        const data = await res.json().catch(() => []);
        const items = Array.isArray(data) ? data : [];
        purchaseBatchActiveIndex = -1;
        renderBatchDropdown(items, typed);
    } catch {
        purchaseBatchSuggestions = [];
        purchaseBatchActiveIndex = -1;
        renderBatchDropdown([], typed);
    }
}, 180));

$('#purchaseBatchSearch').on('keydown', function (e) {
    const $dd = $('#purchaseBatchDropdown');
    const isOpen = $dd.is(':visible') && $dd.find('.autocomplete-item').length > 0;
    if (!isOpen) return;

    if (e.key === 'ArrowDown') {
        e.preventDefault();
        setPurchaseBatchActiveIndex(purchaseBatchActiveIndex + 1);
        return;
    }

    if (e.key === 'ArrowUp') {
        e.preventDefault();
        setPurchaseBatchActiveIndex(purchaseBatchActiveIndex - 1);
        return;
    }

    if (e.key === 'Enter') {
        if (purchaseBatchActiveIndex < 0 || purchaseBatchActiveIndex >= purchaseBatchSuggestions.length) return;
        e.preventDefault();
        const selected = purchaseBatchSuggestions[purchaseBatchActiveIndex];
        if (selected) {
            window.selectPurchaseBatch(selected);
            $('#purchaseEntryQty').focus();
        }
        return;
    }

    if (e.key === 'Escape') {
        e.preventDefault();
        $dd.hide().empty();
        purchaseBatchSuggestions = [];
        purchaseBatchActiveIndex = -1;
    }
});

window.addPurchaseEntryItem = function () {
    if (!purchaseEntryProduct) {
        showToast('Select a product', 'warning');
        return;
    }

    const batchNumber = ($('#purchaseBatchSearch').val() || '').trim();
    if (!batchNumber) {
        showToast('Batch No is required', 'warning');
        $('#purchaseBatchSearch').focus();
        return;
    }

    const unit = ($('#purchaseUnit').val() || '').trim();
    if (!unit) {
        showToast('Unit is required', 'warning');
        return;
    }

    const qty = parseFloat($('#purchaseEntryQty').val()) || 0;
    if (qty <= 0) {
        showToast('Qty is required', 'warning');
        $('#purchaseEntryQty').focus();
        return;
    }

    const rate = parseFloat($('#purchaseEntryRate').val()) || 0;
    const mrp = parseFloat($('#purchaseEntryMrp').val()) || 0;
    const discPct = Math.max(0, parseFloat($('#purchaseEntryDiscPct').val()) || 0);
    const gstPct = Math.max(0, parseFloat($('#purchaseEntryGstPct').val()) || 0);

    const it = {
        productId: purchaseEntryProduct.productId,
        productName: purchaseEntryProduct.productName,
        manufacturer: purchaseEntryProduct.manufacturer,
        batchNumber: batchNumber,
        expiryDate: purchaseEntryBatchStock && purchaseEntryBatchStock.expiryDate
            ? (purchaseEntryBatchStock.expiryDate.substring ? purchaseEntryBatchStock.expiryDate.substring(0, 10) : String(purchaseEntryBatchStock.expiryDate).substring(0, 10))
            : '',
        unit,
        qty: qty,
        purchaseRate: rate,
        mrp: mrp,
        discountPercent: discPct,
        gstPercent: gstPct,
        taxable: 0,
        discountAmount: 0,
        taxAmount: 0,
        lineTotal: 0
    };

    const line = calcLine(it);
    it.taxable = line.afterDisc;
    it.discountAmount = line.discAmt;
    it.taxAmount = line.taxAmt;
    it.lineTotal = line.total;

    purchaseItems.push(it);
    renderPurchaseItems();
    clearPurchaseEntry();
    $('#purchaseProductSearch').focus();
};

$('#purchaseProductSearch').on('input', debounce(async function () {
    const q = ($(this).val() || '').trim();
    if (q.length < 2) {
        $('#purchaseProductDropdown').hide().empty();
        purchaseProductSuggestions = [];
        purchaseProductActiveIndex = -1;
        return;
    }

    try {
        const res = await fetch(`/api/api/products/search?q=${encodeURIComponent(q)}`);
        if (!res.ok) {
            renderProductDropdown([]);
            return;
        }
        const data = await res.json().catch(() => []);
        const items = Array.isArray(data) ? data : [];
        purchaseProductActiveIndex = -1;
        renderProductDropdown(items);
    } catch {
        purchaseProductSuggestions = [];
        purchaseProductActiveIndex = -1;
        renderProductDropdown([]);
    }
}, 180));

$('#purchaseProductSearch').on('keydown', function (e) {
    const $dd = $('#purchaseProductDropdown');
    const isOpen = $dd.is(':visible') && $dd.find('.autocomplete-item').length > 0;
    if (!isOpen) return;

    if (e.key === 'ArrowDown') {
        e.preventDefault();
        setPurchaseProductActiveIndex(purchaseProductActiveIndex + 1);
        return;
    }

    if (e.key === 'ArrowUp') {
        e.preventDefault();
        setPurchaseProductActiveIndex(purchaseProductActiveIndex - 1);
        return;
    }

    if (e.key === 'Enter') {
        if (purchaseProductActiveIndex < 0 || purchaseProductActiveIndex >= purchaseProductSuggestions.length) return;
        e.preventDefault();
        const selected = purchaseProductSuggestions[purchaseProductActiveIndex];
        if (selected) {
            window.selectProduct(selected);
            $('#purchaseBatchSearch').focus();
        }
        return;
    }

    if (e.key === 'Escape') {
        e.preventDefault();
        $dd.hide().empty();
        purchaseProductSuggestions = [];
        purchaseProductActiveIndex = -1;
    }
});

$(document).on('click', function (e) {
    if (!$(e.target).closest('#purchaseProductSearch, #purchaseProductDropdown').length) {
        $('#purchaseProductDropdown').hide();
    }
    if (!$(e.target).closest('#purchaseBatchSearch, #purchaseBatchDropdown').length) {
        $('#purchaseBatchDropdown').hide();
    }
    if (!$(e.target).closest('#supplierSearch, #supplierDropdown').length) {
        $('#supplierDropdown').hide();
    }
});

function focusNextPurchaseEntryField(currentId) {
    const order = [
        'purchaseEntryQty',
        'purchaseEntryRate',
        'purchaseEntryMrp',
        'purchaseEntryDiscPct',
        'purchaseEntryGstPct',
        'purchaseAddItemBtn'
    ];

    const idx = order.indexOf(currentId);
    if (idx < 0) return;
    const nextId = order[Math.min(order.length - 1, idx + 1)];

    if (nextId === 'purchaseAddItemBtn') {
        $('#purchaseAddItemBtn').focus();
    } else {
        $('#' + nextId).focus().select?.();
    }
}

$('#purchaseEntryQty, #purchaseEntryRate, #purchaseEntryMrp, #purchaseEntryDiscPct, #purchaseEntryGstPct').on('keydown', function (e) {
    if (e.key !== 'Enter') return;
    e.preventDefault();

    const id = this.id;
    if (id === 'purchaseEntryGstPct') {
        $('#purchaseAddItemBtn').trigger('click');
        return;
    }

    focusNextPurchaseEntryField(id);
});

function renderSupplierDropdown(items) {
    const $dd = $('#supplierDropdown');
    if (!items.length) {
        $dd.hide().empty();
        supplierSuggestions = [];
        supplierActiveIndex = -1;
        return;
    }

    supplierSuggestions = items;
    if (supplierActiveIndex < 0 || supplierActiveIndex >= items.length) supplierActiveIndex = 0;

    const html = items.map((s, idx) => `
        <div class="autocomplete-item ${idx === supplierActiveIndex ? 'active' : ''}" onclick='selectSupplier(${JSON.stringify(s).replace(/'/g, "\\'")})'>
            <div class="item-main">${s.name}</div>
            <div class="item-sub">${s.phone || ''}</div>
        </div>`).join('');

    $dd.html(html).show();
}

window.selectSupplier = function (s) {
    const name = s && s.name ? s.name : '';
    $('#supplierSearch').val(name);
    $('#supplierDropdown').hide().empty();

    supplierSuggestions = [];
    supplierActiveIndex = -1;

    // Move user forward immediately; linking can complete asynchronously.
    $('#supplierInvoiceNo').focus();

    if (s && s.masterUniqueId) {
        fetch('/api/api/suppliers/from-master', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ masterUniqueId: s.masterUniqueId, MasterUniqueId: s.masterUniqueId })
        })
            .then(async r => {
                const data = await r.json().catch(() => null);
                if (!r.ok || !data) throw new Error((data && data.message) ? data.message : 'Failed to select supplier');
                return data;
            })
            .then(data => {
                selectedSupplier = data;
                $('#supplierSearch').val(data.name || name);
            })
            .catch(err => {
                // If linking fails, keep the supplier name so purchase can still proceed (saved by name).
                selectedSupplier = null;
                $('#supplierSearch').val(name);
                showToast('Supplier selected, but linking failed. It will be saved by name.', 'warning');
            });
        return;
    }

    selectedSupplier = s;
};

$('#supplierSearch').on('keydown', function (e) {
    const $dd = $('#supplierDropdown');
    if (!$dd.is(':visible') || !Array.isArray(supplierSuggestions) || supplierSuggestions.length === 0) return;

    if (e.key === 'ArrowDown') {
        e.preventDefault();
        supplierActiveIndex = supplierActiveIndex < 0 ? 0 : (supplierActiveIndex + 1) % supplierSuggestions.length;
        renderSupplierDropdown(supplierSuggestions);
        return;
    }

    if (e.key === 'ArrowUp') {
        e.preventDefault();
        supplierActiveIndex = supplierActiveIndex < 0
            ? (supplierSuggestions.length - 1)
            : (supplierActiveIndex - 1 + supplierSuggestions.length) % supplierSuggestions.length;
        renderSupplierDropdown(supplierSuggestions);
        return;
    }

    if (e.key === 'Enter') {
        if (supplierActiveIndex < 0 || supplierActiveIndex >= supplierSuggestions.length) return;
        e.preventDefault();
        const s = supplierSuggestions[supplierActiveIndex];
        if (!s) return;
        window.selectSupplier(s);
        return;
    }

    if (e.key === 'Escape') {
        e.preventDefault();
        $dd.hide().empty();
        supplierSuggestions = [];
        supplierActiveIndex = -1;
        return;
    }
});

function toggleNewSupplierForm() {
    const $form = $('#newSupplierForm');
    const isOpen = $form.is(':visible');
    if (isOpen) {
        $form.hide();
        $('#newSupplierName').val('');
        $('#newSupplierPhone').val('');
        return;
    }

    $form.show();
    $('#newSupplierName').focus();
}

async function createNewSupplier() {
    const name = ($('#newSupplierName').val() || '').trim();
    const phone = ($('#newSupplierPhone').val() || '').trim();

    if (!name) {
        showToast('Supplier name is required', 'warning');
        return;
    }

    try {
        const res = await fetch('/api/api/suppliers', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, phone: phone || null })
        });

        const data = await res.json().catch(() => null);
        if (!res.ok || !data) {
            showToast((data && data.message) ? data.message : 'Failed to create supplier', 'error');
            return;
        }

        selectedSupplier = data;
        $('#supplierSearch').val(data.name);
        $('#supplierDropdown').hide().empty();
        toggleNewSupplierForm();
        showToast('Supplier created', 'success');
    } catch {
        showToast('Error creating supplier', 'error');
    }
}

$('#supplierSearch').on('input', debounce(async function () {
    const q = $(this).val().trim();
    if (q.length < 2) {
        $('#supplierDropdown').hide().empty();
        return;
    }

    try {
        const res = await fetch(`/api/api/suppliers/search?q=${encodeURIComponent(q)}`);
        if (!res.ok) {
            renderSupplierDropdown([]);
            return;
        }
        const data = await res.json();
        renderSupplierDropdown(Array.isArray(data) ? data : []);
    } catch {
        renderSupplierDropdown([]);
    }
}, 200));

function recalcPurchaseSummary() {
    let subtotal = 0;
    let tax = 0;

    purchaseItems.forEach(it => {
        subtotal += parseFloat(it.taxable) || 0;
        tax += parseFloat(it.taxAmount) || 0;
    });

    const addDisc = parseFloat($('#purchaseAdditionalDiscount').val()) || 0;
    const roundOff = parseFloat($('#purchaseRoundOff').val()) || 0;

    const gross = Math.max(0, subtotal - addDisc);
    const grandTotal = Math.max(0, gross + tax + roundOff);

    $('#purchaseSubtotal').text(formatCurrency(subtotal));
    $('#purchaseTax').text(formatCurrency(tax));
    $('#purchaseGrandTotal').text(formatCurrency(grandTotal));

    $('#purchaseCreditAmount').val(formatCurrency(grandTotal));
    $('#purchaseBalanceDue').text(formatCurrency(grandTotal));
}

function getGrandTotal() {
    const subtotalText = $('#purchaseSubtotal').text();
    const taxText = $('#purchaseTax').text();

    const subtotal = parseFloat((subtotalText || '').replace('₹', '').trim()) || 0;
    const tax = parseFloat((taxText || '').replace('₹', '').trim()) || 0;

    const addDisc = parseFloat($('#purchaseAdditionalDiscount').val()) || 0;
    const roundOff = parseFloat($('#purchaseRoundOff').val()) || 0;

    return Math.max(0, (Math.max(0, subtotal - addDisc) + tax + roundOff));
}


$('#purchaseAdditionalDiscount, #purchaseRoundOff').on('input', debounce(function () {
    recalcPurchaseSummary();
}, 150));

async function savePurchase() {
    if (!purchaseItems.length) {
        showToast('Add at least one item', 'warning');
        return;
    }

    const refDate = ($('#purchaseRefDate').val() || '').trim();
    if (!refDate) {
        showToast('Ref Date is required', 'warning');
        return;
    }

    const supplierInvoiceNo = ($('#supplierInvoiceNo').val() || '').trim();
    if (!supplierInvoiceNo) {
        showToast('Supplier Invoice No is required', 'warning');
        return;
    }

    const supplierName = ($('#supplierSearch').val() || '').trim();
    if (!selectedSupplier && !supplierName) {
        showToast('Supplier is required', 'warning');
        return;
    }

    const payments = [];

    const payload = {
        supplierId: selectedSupplier ? selectedSupplier.id : null,
        supplierName: selectedSupplier ? selectedSupplier.name : supplierName,
        supplierInvoiceNo: supplierInvoiceNo,
        invoiceDate: new Date().toISOString(),
        refDate: new Date(refDate).toISOString(),
        dueDate: null,
        additionalDiscountAmount: parseFloat($('#purchaseAdditionalDiscount').val()) || 0,
        roundOff: parseFloat($('#purchaseRoundOff').val()) || 0,
        items: purchaseItems.map(it => ({
            productId: it.productId,
            productName: it.productName,
            batchNumber: (it.batchNumber || '').trim() || null,
            expiryDate: it.expiryDate ? new Date(it.expiryDate).toISOString() : null,
            qty: parseFloat(it.qty) || 0,
            purchaseRate: parseFloat(it.purchaseRate) || 0,
            mrp: parseFloat(it.mrp) || 0,
            discountPercent: parseFloat(it.discountPercent) || 0,
            gstPercent: parseFloat(it.gstPercent) || 0
        })),
        payments
    };

    try {
        $('#savePurchaseBtn').prop('disabled', true);
        const res = await fetch('/api/api/purchases/complete', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        const data = await res.json().catch(() => null);

        if (!res.ok) {
            showToast((data && data.message) ? data.message : 'Failed to save purchase', 'error');
            return;
        }

        showToast('Purchase saved', 'success');
        resetPurchase();
    } catch (e) {
        showToast('Error saving purchase', 'error');
    } finally {
        $('#savePurchaseBtn').prop('disabled', false);
    }
}

function resetPurchase() {
    purchaseItems = [];
    selectedSupplier = null;
    $('#supplierSearch').val('');
    $('#supplierInvoiceNo').val('');
    $('#purchaseAdditionalDiscount').val('0');
    $('#purchaseRoundOff').val('0');
    $('#purchaseCreditAmount').val(formatCurrency(0));
    setTodayDates();
    clearPurchaseEntry();
    renderPurchaseItems();
    recalcPurchaseSummary();
}

$(function () {
    setTodayDates();
    clearPurchaseEntry();
    renderPurchaseItems();
});
