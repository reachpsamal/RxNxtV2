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

const API_BASE = '/api/api';

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
    $.get(`${API_BASE}/customers/search?q=${encodeURIComponent(q)}`, function (data) {
        const dropdown = $('#customerDropdown');
        dropdown.empty();
        if (data.length === 0) {
            dropdown.html('<div class="autocomplete-no-results"><i class="bi bi-person-x"></i> No customer found. Try adding a new one.</div>');
        } else {
            data.forEach(c => {
                dropdown.append(`
                    <div class="autocomplete-item" onclick="selectCustomer(${c.id})">
                        <div class="item-name">${c.name}</div>
                        <div class="item-detail">
                            <i class="bi bi-telephone"></i> ${c.phone}
                            ${c.email ? ' &middot; <i class="bi bi-envelope"></i> ' + c.email : ''}
                        </div>
                    </div>
                `);
            });
        }
        dropdown.addClass('show');
    });
}));

function selectCustomer(id) {
    $.get(`${API_BASE}/customers/${id}`, function (data) {
        selectedCustomer = data;
        const initials = data.name.split(' ').map(n => n[0]).join('').toUpperCase().substring(0, 2);
        $('#selectedCustomerCard').html(`
            <div class="customer-card">
                <div class="customer-avatar">${initials}</div>
                <div class="customer-info">
                    <div class="customer-name"><i class="bi bi-person"></i> ${data.name} &middot; <i class="bi bi-telephone"></i> ${data.phone}</div>
                </div>
                <button class="customer-remove" onclick="removeCustomer()" title="Remove Customer">
                    <i class="bi bi-x-lg"></i>
                </button>
            </div>
        `).show();
        $('#customerDropdown').removeClass('show');
        $('#customerSearch').val('').hide();
        $('#toggleNewCustomerForm').hide();
        $('#skipCustomerBtn').hide();
        showToast(`Customer "${data.name}" selected`, 'success');
        updateCompleteSaleBtn();
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

function saveNewCustomer() {
    const name = $('#newCustName').val().trim();
    const phone = $('#newCustPhone').val().trim();

    if (!name) { showToast('Customer name is required', 'error'); return; }
    if (!phone) { showToast('Phone number is required', 'error'); return; }

    $.ajax({
        url: `${API_BASE}/customers`,
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ name, phone }),
        success: function (data) {
            showToast(`Customer "${data.name}" created successfully!`, 'success');
            selectCustomer(data.id);
            $('#newCustomerForm').removeClass('show');
            // Clear form
            $('#newCustName, #newCustPhone').val('');
        },
        error: function (xhr) {
            const msg = xhr.responseJSON?.message || 'Failed to create customer';
            showToast(msg, 'error');
        }
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

// ============ BATCH SEARCH (TAB A) ============
function fetchBatchSearchResults(q, onDone) {
    $.get(`${API_BASE}/batches/search?q=${encodeURIComponent(q)}`, function (data) {
        onDone(data || []);
    }).fail(function () {
        onDone([]);
    });
}

function renderBatchDropdown(data) {
    const dropdown = $('#batchDropdown');
    dropdown.empty();
    if (data.length === 0) {
        dropdown.html('<div class="autocomplete-no-results"><i class="bi bi-box"></i> No batch found</div>');
    } else {
        data.forEach(b => {
            const expiryClass = b.isExpired ? 'badge-expired' : (b.isNearExpiry ? 'badge-expiry-warning' : 'badge-stock');
            const expiryText = b.isExpired ? 'EXPIRED' : (b.isNearExpiry ? 'Near Expiry' : 'Valid');
            const productName = readStockProductName(b);
            const batchNumber = readStockBatchNumber(b);
            const qty = readStockQty(b);
            const uom = readStockUom(b);
            dropdown.append(`
                    <div class="autocomplete-item" onclick="selectStock('${b.productId}', '${b.batchNumber}', 'batch')">
                        <div class="item-name">${productName}</div>
                        <div class="item-detail">
                            Batch: <strong>${batchNumber}</strong> &middot; 
                            Exp: ${formatExpiryDate(b.expiryDate) || '-'} 
                            <span class="item-badge ${expiryClass}">${expiryText}</span> &middot;
                            Stock: ${qty} ${uom}
                        </div>
                    </div>
                `);
        });
    }
    dropdown.addClass('show');
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
            selectStock(exact.productId, exact.batchNumber, 'batch');
            return;
        }

        // If the query is a prefix of a single batchNumber (common while typing/scanning), auto-select it
        const prefixMatches = data.filter(b => (b.batchNumber || '').toLowerCase().startsWith(query.toLowerCase()));
        if (prefixMatches.length === 1) {
            $('#batchDropdown').removeClass('show').empty();
            selectStock(prefixMatches[0].productId, prefixMatches[0].batchNumber, 'batch');
            return;
        }

        if (data.length === 1) {
            $('#batchDropdown').removeClass('show').empty();
            selectStock(data[0].productId, data[0].batchNumber, 'batch');
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
    $.get(`${API_BASE}/stocks/by-product-batch?productId=${encodeURIComponent(productId)}&batchNumber=${encodeURIComponent(batchNumber)}`, function (b) {
        currentBatchInfo = b;
        const expiryStr = formatExpiryDate(b.expiryDate) || '-';
        const expiryClass = b.isExpired ? 'danger' : (b.isNearExpiry ? 'warning' : '');
        const expiryWarning = b.isExpired ? '<span style="color:var(--danger-500);font-weight:600;font-size:0.78rem;">⚠ EXPIRED</span>' :
            (b.isNearExpiry ? '<span style="color:var(--warning-500);font-weight:600;font-size:0.78rem;">⚠ Near Expiry</span>' : '');

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
                    <div class="batch-info-value">${formatCurrency(b.mrp)}</div>
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
    const overallDiscPercent = parseFloat($('#overallDiscountPercent').val()) || 0;
    const overallDiscAmount = (subTotal - itemDiscount) * overallDiscPercent / 100;
    const taxTotal = getTaxTotal();
    const payable = Math.max(0, subTotal - itemDiscount - overallDiscAmount + taxTotal);

    $('#saleSummaryItems').val(`${saleItems.length}`);
    $('#saleSummaryMrp').val(formatCurrency(subTotal));
    $('#saleSummaryDiscountAmt').val(formatCurrency(itemDiscount + overallDiscAmount));
    $('#saleSummaryTax').val(formatCurrency(taxTotal));
    $('#saleSummaryPayable').val(formatCurrency(payable));

    $('#saleItemsSummary').show();
}

function syncOverallDiscountToAdditionalDiscount() {
    const subTotal = getSubTotal();
    let itemDiscount = 0;
    saleItems.forEach(item => {
        itemDiscount += item.discountAmount || 0;
    });
    const overallDiscPercent = parseFloat($('#overallDiscountPercent').val()) || 0;
    const overallDiscAmount = (subTotal - itemDiscount) * overallDiscPercent / 100;
    $('#additionalDiscount').val(overallDiscAmount.toFixed(2));
}

$('#overallDiscountPercent').on('input change', function () {
    const disc = parseFloat($(this).val()) || 0;
    if (disc < 0 || disc > 100) {
        showToast('Discount must be between 0 and 100%', 'error');
        $(this).val(Math.min(100, Math.max(0, disc)));
    }
    syncOverallDiscountToAdditionalDiscount();
    recalculateBill();
    updateSaleItemsSummary();
});

$('#additionalDiscount').on('input change', function () {
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
    $.get(`${API_BASE}/medicines/search?q=${encodeURIComponent(q)}`, function (data) {
        const dropdown = $('#medicineDropdown');
        dropdown.empty();
        if (data.length === 0) {
            dropdown.html('<div class="autocomplete-no-results"><i class="bi bi-capsule"></i> No medicine found</div>');
        } else {
            data.forEach(m => {
                const qty = readStockQty(m);
                const uom = readStockUom(m);
                dropdown.append(`
                    <div class="autocomplete-item" onclick="selectStock('${m.productId}', '${m.batchNumber}', 'direct')">
                        <div class="item-name">${m.productName}</div>
                        <div class="item-detail">
                            ${m.manufacturer || ''} &middot; Batch: <strong>${m.batchNumber}</strong> &middot; Stock: ${qty} ${uom}
                        </div>
                    </div>
                `);
            });
        }
        dropdown.addClass('show');
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
        saleItems[existingIndex].price = currentBatchInfo.mrp;
        saleItems[existingIndex].availableQty = maxQty;
        recalculateItem(existingIndex);
        showToast(`Updated ${currentBatchInfo.productName} quantity to ${qty} ${unitType.toLowerCase()}s`, 'info');
    } else {
        const price = currentBatchInfo.mrp;
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
            taxPercent: 5,
            taxAmount: (price * qty) * 0.05,
            total: (price * qty) + ((price * qty) * 0.05),
            availableQty: maxQty
        });
        showToast(`Added ${currentBatchInfo.productName} x${qty} ${unitType.toLowerCase()}s`, 'success');
    }

    renderSaleItems();
    recalculateBill();
    syncOverallDiscountToAdditionalDiscount();
    updateSaleItemsSummary();
    updateCompleteSaleBtn();

    // Clear batch selection
    currentBatchInfo = null;
    selectedUnitType = 'PCS';
    $('#batchInfoCard, #medicineBatchesCard').removeClass('show').empty();
    $('#batchSearch, #medicineSearch').val('');
}

function recalculateItem(index) {
    const item = saleItems[index];
    const lineTotal = item.price * item.quantity;
    item.discountAmount = lineTotal * item.discountPercent / 100;
    const taxable = lineTotal - item.discountAmount;
    item.taxPercent = FIXED_TAX_PERCENT;
    item.taxAmount = taxable * FIXED_TAX_PERCENT / 100;
    item.total = taxable + item.taxAmount;
}

function formatExpiryDate(expiryDate) {
    if (!expiryDate) return '';
    const d = new Date(expiryDate);
    if (Number.isNaN(d.getTime())) return '';
    return d.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' });
}

function getMaxQtyForItem(item) {
    return item.availableQty ?? 0;
}

function updateItemUnitType(index, value) {
    const item = saleItems[index];
    item.unitType = item.uomName || value;

    const maxQty = getMaxQtyForItem(item);
    if (item.quantity > maxQty) {
        item.quantity = Math.max(1, maxQty);
        showToast(`Quantity adjusted to available stock (${maxQty})`, 'info');
    }

    recalculateItem(index);
    renderSaleItems();
    recalculateBill();
    syncOverallDiscountToAdditionalDiscount();
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
        tbody.append(`
            <tr class="row-highlight">
                <td style="color:var(--gray-400); font-weight:600;">${index + 1}</td>
                <td>
                    <div style="font-weight:600;">${item.productName}</div>
                </td>
                <td>
                    <div style="font-size:0.78rem; color: var(--gray-600); font-weight:700;">${item.batchNumber}</div>
                    <div style="font-size:0.72rem; color: var(--gray-400); margin-top:2px;">Exp: ${formatExpiryDate(item.expiryDate)}</div>
                </td>
                <td>
                    <select class="item-unit-select" onchange="updateItemUnitType(${index}, this.value)" id="unit-${index}">
                        <option value="${item.uomName}" selected>${item.uomName}</option>
                    </select>
                </td>
                <td>
                    <input type="number" class="item-qty-input" value="${item.quantity}" min="1" max="${getMaxQtyForItem(item)}"
                           onchange="updateItemQuantity(${index}, this.value)" id="qty-${index}">
                </td>
                <td style="font-variant-numeric: tabular-nums;">${formatCurrency(item.price)}</td>
                <td>
                    <input type="number" class="item-discount-input" value="${item.discountPercent}" min="0" max="100" step="0.5"
                           onchange="updateItemDiscount(${index}, this.value)" id="disc-${index}">
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

    // Keep overall discount amount aligned with subtotal changes
    syncOverallDiscountToAdditionalDiscount();
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

    syncOverallDiscountToAdditionalDiscount();
    updateSaleItemsSummary();
}

function removeItem(index) {
    const name = saleItems[index].productName;
    saleItems.splice(index, 1);
    renderSaleItems();
    recalculateBill();
    syncOverallDiscountToAdditionalDiscount();
    updateSaleItemsSummary();
    updateCompleteSaleBtn();
    showToast(`Removed ${name}`, 'info');
}

function clearAllItems() {
    saleItems = [];
    renderSaleItems();
    recalculateBill();
    $('#overallDiscountPercent').val(0);
    syncOverallDiscountToAdditionalDiscount();
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
        subTotal += item.price * item.quantity;
        itemDiscount += item.discountAmount;
        taxTotal += item.taxAmount || 0;
    });

    const additionalDiscount = parseFloat($('#additionalDiscount').val()) || 0;
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
    $('#billItemDiscount').text('- ' + formatCurrency(itemDiscount));
    $('#billGrandTotal').text(formatCurrency(displayGrandTotal));

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
        subTotal += item.price * item.quantity;
        itemDiscount += item.discountAmount;
        taxTotal += item.taxAmount || 0;
    });
    const additionalDiscount = parseFloat($('#additionalDiscount').val()) || 0;
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

    $.get(`${API_BASE}/batches/advanced-search?${params.join('&')}`, function (data) {
        const container = $('#advancedSearchResults');
        if (data.length === 0) {
            container.html('<div class="autocomplete-no-results" style="padding:20px;"><i class="bi bi-inbox" style="font-size:2rem;display:block;margin-bottom:8px;"></i> No batches found</div>');
            return;
        }

        let html = `<table class="batch-results-table"><thead><tr>
            <th>Product</th><th>Batch</th><th>Expiry</th><th>Stock</th><th>MRP</th><th>UOM</th><th></th>
        </tr></thead><tbody>`;
        data.forEach(b => {
            const expiryStyle = b.isExpired ? 'color:var(--danger-500);font-weight:600' : (b.isNearExpiry ? 'color:var(--warning-500);font-weight:600' : '');
            const qty = readStockQty(b);
            const uom = readStockUom(b);
            html += `<tr>
                <td><strong>${b.productName}</strong><br><span style="font-size:0.72rem;color:var(--gray-400);">${b.manufacturer || ''}</span></td>
                <td>${b.batchNumber}</td>
                <td style="${expiryStyle}">${formatExpiryDate(b.expiryDate) || '-'}</td>
                <td>${qty}</td>
                <td>${formatCurrency(b.mrp)}</td>
                <td>${uom}</td>
                <td><button class="btn-add-cart" onclick="selectStock('${b.productId}', '${b.batchNumber}', 'direct')"><i class="bi bi-cart-plus"></i> Add</button></td>
            </tr>`;
        });
        html += '</tbody></table>';
        container.html(html);
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

    // Calculate roundoff
    let s = 0, d = 0, t = 0;
    saleItems.forEach(i => { s += i.price * i.quantity; d += i.discountAmount; t += i.taxAmount || 0; });
    const addDisc = parseFloat($('#additionalDiscount').val()) || 0;
    const baseGrand = Math.max(0, s - d - addDisc + t);
    const roundOff = parseFloat((grandTotal - baseGrand).toFixed(2));

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
            taxPercent: FIXED_TAX_PERCENT
        })),
        additionalDiscount: parseFloat($('#additionalDiscount').val()) || 0,
        roundOff: roundOff,
        payments: payments
    };

    // Disable button and show loading
    $('#completeSaleBtn').prop('disabled', true).html('<span class="spinner-pharmacy"></span> Processing...');

    $.ajax({
        url: `${API_BASE}/sales/complete`,
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(request),
        success: function (result) {
            if (result.success) {
                $('#successInvoice').text(result.invoiceNumber);
                $('#successOverlay').addClass('show');
                showToast('Sale completed!', 'success');
            } else {
                showToast(result.message, 'error');
                $('#completeSaleBtn').prop('disabled', false).html('<i class="bi bi-check-circle"></i> Complete Sale');
            }
        },
        error: function (xhr) {
            const msg = xhr.responseJSON?.message || 'Failed to complete sale';
            showToast(msg, 'error');
            $('#completeSaleBtn').prop('disabled', false).html('<i class="bi bi-check-circle"></i> Complete Sale');
        }
    });
}

function startNewSale() {
    // Reset everything
    selectedCustomer = null;
    saleItems = [];
    currentBatchInfo = null;
    selectedUnitType = 'Strip';
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
