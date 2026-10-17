/**
 * ECO-ARECA Checkout & Payment Controller
 * Handles 2-column checkout modal, live server-side validation,
 * VietQR bank transfer, VNPay sandbox redirection & return handling.
 */

class CheckoutManager {
  constructor() {
    this.modal = null;
    this.form = null;
    this.couponCode = '';
    this.shippingMethod = 0; // 0: StandardDelivery, 1: ShowroomPickup
    this.paymentMethod = 0;  // 0: COD, 1: BankTransfer, 2: VNPay
    this.cachedValidation = null;

    document.addEventListener('DOMContentLoaded', () => this.init());
  }

  init() {
    this.modal = document.getElementById('checkout-modal');
    this.closeBtn = document.getElementById('checkout-modal-close');
    this.form = document.getElementById('checkout-form');
    this.resultModal = document.getElementById('order-result-modal');
    this.resultCloseBtn = document.getElementById('order-result-close');

    if (this.closeBtn) {
      this.closeBtn.addEventListener('click', () => this.close());
    }

    if (this.resultCloseBtn && this.resultModal) {
      this.resultCloseBtn.addEventListener('click', () => {
        this.resultModal.classList.remove('active');
        document.body.style.overflow = '';
      });
    }

    if (this.form) {
      this.form.addEventListener('submit', (e) => this.handleSubmit(e));
    }

    // Check if returning from VNPay
    this.checkVNPayReturnParams();
  }

  open() {
    if (!window.cart || window.cart.items.length === 0) {
      alert("Giỏ hàng của bạn đang trống. Vui lòng chọn sản phẩm trước khi thanh toán!");
      return;
    }

    if (this.modal) {
      this.modal.classList.add('active');
      document.body.style.overflow = 'hidden';
      this.couponCode = window.cart.couponCode || '';
      this.render();
      this.validateWithServer();
    }
  }

  close() {
    if (this.modal) {
      this.modal.classList.remove('active');
      document.body.style.overflow = '';
    }
  }

  setShippingMethod(method) {
    this.shippingMethod = parseInt(method, 10);
    this.validateWithServer();
  }

  setPaymentMethod(method) {
    this.paymentMethod = parseInt(method, 10);
    
    // Update card selection styles
    document.querySelectorAll('.payment-option-card').forEach(card => {
      card.classList.remove('selected');
    });
    const activeCard = document.querySelector(`.payment-option-card[data-method="${method}"]`);
    if (activeCard) activeCard.classList.add('selected');
  }

  async applyCoupon() {
    const input = document.getElementById('checkout-coupon-input');
    if (!input) return;

    const code = input.value.trim().toUpperCase();
    if (!code) {
      this.couponCode = '';
      this.validateWithServer();
      return;
    }

    this.couponCode = code;
    await this.validateWithServer();
  }

  removeCoupon() {
    this.couponCode = '';
    if (window.cart) window.cart.couponCode = '';
    const input = document.getElementById('checkout-coupon-input');
    if (input) input.value = '';
    this.validateWithServer();
  }

  async validateWithServer() {
    if (!window.cart || window.cart.items.length === 0) return;

    const payload = {
      items: window.cart.items.map(item => ({
        productId: item.productId,
        variantLabel: item.variantLabel,
        quantity: item.quantity
      })),
      couponCode: this.couponCode,
      shippingMethod: this.shippingMethod
    };

    try {
      const res = await fetch('/api/order/validate-cart', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (res.ok) {
        this.cachedValidation = await res.json();
        this.updateSummaryUI(this.cachedValidation);
        return;
      }
    } catch (e) {
      console.warn("Server validation not reachable, using local calculation fallback", e);
    }

    // Local calculation fallback (for GitHub Pages / offline mode)
    this.cachedValidation = this.calculateLocalValidation();
    this.updateSummaryUI(this.cachedValidation);
  }

  calculateLocalValidation() {
    if (!window.cart || !window.cart.items) {
      return { isValid: false, subtotal: 0, shippingFee: 0, discountAmount: 0, totalAmount: 0, appliedCoupon: null };
    }
    const subtotal = window.cart.items.reduce((sum, item) => sum + (item.price * item.quantity), 0);
    const shippingFee = (this.shippingMethod === 1 || subtotal >= 300000) ? 0 : 30000;
    let discount = 0;
    const code = (this.couponCode || '').toUpperCase().trim();
    if (code === 'ECOARECA10') discount = Math.round(subtotal * 0.1);
    else if (code === 'GIAM10K') discount = 10000;
    else if (code === 'FREESHIP') discount = (subtotal < 300000 && this.shippingMethod !== 1) ? 30000 : 0;
    discount = Math.min(discount, subtotal);
    const total = Math.max(0, subtotal - discount + shippingFee);
    return {
      isValid: true,
      subtotal: subtotal,
      shippingFee: shippingFee,
      discountAmount: discount,
      totalAmount: total,
      appliedCoupon: discount > 0 ? code : null
    };
  }

  updateSummaryUI(val) {
    const subtotalEl = document.getElementById('checkout-subtotal');
    const discountEl = document.getElementById('checkout-discount');
    const discountRow = document.getElementById('checkout-discount-row');
    const shippingEl = document.getElementById('checkout-shipping');
    const totalEl = document.getElementById('checkout-total');
    const couponBadge = document.getElementById('checkout-coupon-badge');
    const couponInput = document.getElementById('checkout-coupon-input');

    if (subtotalEl) subtotalEl.textContent = formatCurrency(val.subtotal);

    if (discountRow) {
      if (val.discountAmount > 0) {
        discountRow.style.display = 'flex';
        if (discountEl) discountEl.textContent = `-${formatCurrency(val.discountAmount)}`;
      } else {
        discountRow.style.display = 'none';
      }
    }

    if (couponBadge) {
      if (val.appliedCoupon) {
        couponBadge.style.display = 'inline-flex';
        couponBadge.innerHTML = `
          <span>Mã giảm: <strong>${val.appliedCoupon}</strong></span>
          <button type="button" onclick="window.checkout.removeCoupon()">&times;</button>
        `;
        if (couponInput) couponInput.value = val.appliedCoupon;
      } else {
        couponBadge.style.display = 'none';
      }
    }

    if (shippingEl) {
      if (val.shippingFee === 0) {
        shippingEl.innerHTML = `<span style="color:#2E7D32; font-weight:700;">Miễn phí</span>`;
      } else {
        shippingEl.textContent = formatCurrency(val.shippingFee);
      }
    }

    if (totalEl) totalEl.textContent = formatCurrency(val.totalAmount);
  }

  render() {
    const list = document.getElementById('checkout-items-summary');
    if (!list || !window.cart) return;

    list.innerHTML = window.cart.items.map(item => `
      <div class="checkout-item-row">
        <img src="${item.image}" alt="${item.name}" class="checkout-item-thumb">
        <div class="checkout-item-info">
          <div class="checkout-item-name">${item.name}</div>
          <div class="checkout-item-sub">${item.variantLabel} × ${item.quantity}</div>
        </div>
        <div class="checkout-item-price">${formatCurrency(item.price * item.quantity)}</div>
      </div>
    `).join('');
  }

  async handleSubmit(e) {
    e.preventDefault();

    if (!window.cart || window.cart.items.length === 0) {
      alert("Giỏ hàng của bạn đang trống!");
      return;
    }

    const form = e.target;
    const name = form.customer_name.value.trim();
    const phone = form.customer_phone.value.trim();
    const email = form.customer_email ? form.customer_email.value.trim() : '';
    const address = form.customer_address.value.trim();
    const ward = form.customer_ward ? form.customer_ward.value.trim() : '';
    const district = form.customer_district ? form.customer_district.value.trim() : '';
    const province = form.customer_province ? form.customer_province.value.trim() : '';
    const note = form.customer_note ? form.customer_note.value.trim() : '';

    if (!name || name.length < 2) {
      alert("Vui lòng nhập họ và tên hợp lệ (tối thiểu 2 ký tự).");
      form.customer_name.focus();
      return;
    }

    const cleanPhone = phone.replace(/[\s.-]/g, '');
    if (!/^(03|05|07|08|09)\d{8}$/.test(cleanPhone)) {
      alert("Vui lòng nhập số điện thoại Việt Nam hợp lệ gồm 10 chữ số (Ví dụ: 0987112233).");
      form.customer_phone.focus();
      return;
    }

    if (!address) {
      alert("Vui lòng nhập số nhà và tên đường chi tiết.");
      form.customer_address.focus();
      return;
    }

    const submitBtn = document.getElementById('checkout-submit-btn');
    if (submitBtn) {
      submitBtn.disabled = true;
      submitBtn.innerHTML = `
        <span style="display:inline-block; animation: spin 1s linear infinite; margin-right:8px;">⏳</span>
        Đang khởi tạo đơn hàng & thanh toán...
      `;
    }

    const payload = {
      customerName: name,
      phoneNumber: cleanPhone,
      email: email,
      addressDetail: address,
      ward: ward,
      district: district,
      province: province,
      customerNote: note,
      shippingMethod: this.shippingMethod,
      paymentMethod: this.paymentMethod,
      couponCode: this.couponCode,
      items: window.cart.items.map(item => ({
        productId: item.productId,
        variantLabel: item.variantLabel,
        quantity: item.quantity
      }))
    };

    let data = null;
    try {
      const res = await fetch('/api/order/create', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (res.ok) {
        data = await res.json();
      }
    } catch (err) {
      console.warn("Server order creation not available, using offline/demo mode", err);
    }

    if (!data || !data.success) {
      // Offline / GitHub Pages Demonstration Mode
      const val = this.cachedValidation || this.calculateLocalValidation();
      const now = new Date();
      const yy = String(now.getFullYear()).slice(-2);
      const mm = String(now.getMonth() + 1).padStart(2, '0');
      const dd = String(now.getDate()).padStart(2, '0');
      const rand = String(Math.floor(1000 + Math.random() * 9000));
      const orderCode = `ECO-${yy}${mm}${dd}-${rand}`;

      const pmNames = ['COD', 'BankTransfer', 'VNPay'];
      const currentPm = pmNames[this.paymentMethod] || 'COD';

      const bankInfo = (currentPm === 'BankTransfer') ? {
        bankCode: "MB",
        bankName: "MB Bank (Ngân hàng Quân Đội)",
        accountNumber: "0912345678",
        accountName: "NGUYEN VAN A - ECO ARECA",
        transferContent: `${orderCode} Thanh toan don hang`,
        qrCodeUrl: `https://img.vietqr.io/image/MB-0912345678-compact2.png?amount=${val.totalAmount}&addInfo=${encodeURIComponent(orderCode + " Thanh toan don hang")}&accountName=${encodeURIComponent("NGUYEN VAN A - ECO ARECA")}`
      } : null;

      data = {
        success: true,
        orderId: 'local-' + Date.now(),
        orderCode: orderCode,
        totalAmount: val.totalAmount,
        paymentMethod: currentPm,
        paymentStatus: 'Pending',
        bankTransferInfo: bankInfo,
        message: "Đặt hàng thành công!"
      };

      // Save to localStorage for tracking & admin demo
      try {
        const localOrders = JSON.parse(localStorage.getItem('ecoareca_local_orders') || '[]');
        localOrders.unshift({
          id: data.orderId,
          orderCode: orderCode,
          customerName: name,
          phoneNumber: cleanPhone,
          email: email,
          shippingAddress: `${address}${ward ? ', ' + ward : ''}${district ? ', ' + district : ''}${province ? ', ' + province : ''}`,
          customerNote: note,
          subtotal: val.subtotal,
          discountAmount: val.discountAmount,
          shippingFee: val.shippingFee,
          totalAmount: val.totalAmount,
          orderStatus: 'PendingConfirmation',
          paymentMethod: currentPm,
          paymentStatus: 'Pending',
          createdAt: new Date().toISOString(),
          items: window.cart.items.map((item, idx) => ({
            id: `item-${idx}`,
            productId: item.productId,
            productNameSnapshot: item.name,
            variantSnapshot: item.variantLabel,
            unitPriceSnapshot: item.price,
            quantity: item.quantity,
            lineTotal: item.price * item.quantity
          })),
          history: [{
            fromStatus: 'None',
            toStatus: 'PendingConfirmation',
            changedAt: new Date().toISOString(),
            changedBy: 'Khách hàng (Đặt hàng)'
          }],
          bankTransferInfo: bankInfo
        });
        localStorage.setItem('ecoareca_local_orders', JSON.stringify(localOrders));
      } catch (e) {
        console.warn("Could not save to localStorage", e);
      }
    }

    try {
      // Success: clear cart
      window.cart.clearCart();
      this.close();

      // If VNPay: redirect to VNPay Gateway if URL provided
      if (this.paymentMethod === 2 && data.paymentUrl) {
        window.location.href = data.paymentUrl;
        return;
      }

      // If BankTransfer or COD: open Order Confirmation modal
      this.showOrderResult(data);

    } catch (err) {
      console.error("Order completion failed", err);
    } finally {
      if (submitBtn) {
        submitBtn.disabled = false;
        submitBtn.innerHTML = `XÁC NHẬN ĐẶT HÀNG & THANH TOÁN`;
      }
    }
  }

  showOrderResult(data) {
    if (!this.resultModal) return;

    const content = document.getElementById('order-result-content');
    if (!content) return;

    let paymentSpecificHTML = '';

    if (data.paymentMethod === 'BankTransfer' && data.bankTransferInfo) {
      const bank = data.bankTransferInfo;
      paymentSpecificHTML = `
        <div class="vietqr-card-container">
          <div class="vietqr-image-wrap">
            <img src="${bank.qrCodeUrl}" alt="Mã VietQR Thanh Toán" class="vietqr-img">
            <span class="vietqr-img-sub">Mở App Ngân hàng bất kỳ để quét mã</span>
          </div>
          <div class="bank-details-list">
            <div class="bank-detail-item">
              <span class="bank-detail-label">Ngân hàng:</span>
              <span class="bank-detail-val">${bank.bankName}</span>
            </div>
            <div class="bank-detail-item">
              <span class="bank-detail-label">Chủ tài khoản:</span>
              <span class="bank-detail-val">${bank.accountName}</span>
            </div>
            <div class="bank-detail-item">
              <span class="bank-detail-label">Số tài khoản:</span>
              <span class="bank-detail-val">
                <span>${bank.accountNumber}</span>
                <button class="copy-btn" onclick="window.checkout.copyText('${bank.accountNumber}', this)">Sao Chép</button>
              </span>
            </div>
            <div class="bank-detail-item">
              <span class="bank-detail-label">Số tiền chuyển:</span>
              <span class="bank-detail-val highlight">
                <span>${formatCurrency(bank.amount)}</span>
                <button class="copy-btn" onclick="window.checkout.copyText('${bank.amount}', this)">Sao Chép</button>
              </span>
            </div>
            <div class="bank-detail-item">
              <span class="bank-detail-label">Nội dung chuyển:</span>
              <span class="bank-detail-val">
                <span style="color:#C62828;">${bank.transferContent}</span>
                <button class="copy-btn" onclick="window.checkout.copyText('${bank.transferContent}', this)">Sao Chép</button>
              </span>
            </div>
            <div class="bank-notice-box">
              ⚠️ Quý khách vui lòng <strong>giữ nguyên nội dung chuyển khoản</strong> để hệ thống đối soát và xác nhận đơn hàng tự động nhanh nhất.
            </div>
          </div>
        </div>
      `;
    } else if (data.paymentMethod === 'COD') {
      paymentSpecificHTML = `
        <div style="background:#F4F8F4; border:1px solid #D6E8D8; border-radius:12px; padding:20px; margin-top:20px; text-align:left;">
          <h4 style="color:#1E3B2A; margin-bottom:8px; display:flex; align-items:center; gap:8px;">
            <span>💵</span> Thanh toán khi nhận hàng (COD)
          </h4>
          <p style="font-size:0.9rem; color:#5E665E; line-height:1.6; margin-bottom:0;">
            Đơn hàng của bạn đã được ghi nhận vào hệ thống. Đội ngũ chăm sóc khách hàng ECO-ARECA sẽ gọi điện thoại xác nhận đơn hàng và thời gian giao dự kiến trong vòng 2-4 giờ làm việc. Bạn chỉ cần thanh toán đúng số tiền cho nhân viên giao hàng khi nhận và kiểm tra kiện hàng.
          </p>
        </div>
      `;
    }

    content.innerHTML = `
      <div style="text-align:center; padding:10px 0 20px;">
        <div style="width:68px; height:68px; background:#E8F5E9; color:#2E7D32; border-radius:50%; display:flex; align-items:center; justify-content:center; margin:0 auto 16px;">
          <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg>
        </div>

        <h3 style="color:#1E3B2A; font-size:1.6rem; margin-bottom:6px;">Đặt Hàng Thành Công!</h3>
        <p style="color:#5E665E; font-size:0.95rem; margin-bottom:14px;">
          Cảm ơn bạn đã đồng hành cùng sản phẩm mo cau tự nhiên thân thiện môi trường ECO-ARECA.
        </p>

        <div style="display:inline-flex; align-items:center; gap:10px; background:#FAF9F5; border:1px solid #D8C3A5; padding:8px 18px; border-radius:8px; margin-bottom:16px;">
          <span style="font-size:0.9rem; color:#5E665E;">Mã đơn hàng:</span>
          <strong style="font-size:1.15rem; color:#B8860B;">${data.orderCode}</strong>
          <button class="copy-btn" onclick="window.checkout.copyText('${data.orderCode}', this)">Sao Chép</button>
        </div>

        <div style="font-size:1.1rem; color:#1E3B2A; font-weight:700; margin-bottom:20px;">
          Tổng thanh toán: <span style="color:#C49A45;">${formatCurrency(data.totalAmount)}</span>
        </div>

        ${paymentSpecificHTML}

        <div style="display:flex; justify-content:center; gap:14px; margin-top:28px;">
          <button class="btn btn-outline" onclick="window.orderLookup.openWithCode('${data.orderCode}')">
            🔍 Tra Cứu Tiến Độ Đơn
          </button>
          <button class="btn btn-primary" onclick="document.getElementById('order-result-modal').classList.remove('active'); document.body.style.overflow='';">
            Tiếp Tục Mua Sắm
          </button>
        </div>
      </div>
    `;

    this.resultModal.classList.add('active');
    document.body.style.overflow = 'hidden';
  }

  checkVNPayReturnParams() {
    const params = new URLSearchParams(window.location.search);
    const vnpOrder = params.get('vnp_order');
    const vnpSuccess = params.get('vnp_success');
    const vnpCode = params.get('vnp_code');
    const vnpTxn = params.get('vnp_txn');

    if (!vnpOrder) return;

    // Clear query params from URL so refresh doesn't re-trigger
    window.history.replaceState({}, document.title, window.location.pathname);

    if (!this.resultModal) return;
    const content = document.getElementById('order-result-content');
    if (!content) return;

    const isSuccess = vnpSuccess === 'true';

    content.innerHTML = `
      <div style="text-align:center; padding:10px 0 20px;">
        <div style="width:68px; height:68px; background:${isSuccess ? '#E8F5E9' : '#FFEBEE'}; color:${isSuccess ? '#2E7D32' : '#C62828'}; border-radius:50%; display:flex; align-items:center; justify-content:center; margin:0 auto 16px;">
          ${isSuccess 
            ? `<svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg>`
            : `<svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>`
          }
        </div>

        <h3 style="color:#1E3B2A; font-size:1.6rem; margin-bottom:6px;">
          ${isSuccess ? 'Thanh Toán VNPay Thành Công!' : 'Thanh Toán VNPay Không Thành Công'}
        </h3>
        <p style="color:#5E665E; font-size:0.95rem; margin-bottom:14px;">
          ${isSuccess 
            ? 'Giao dịch qua cổng thanh toán VNPay đã được xử lý và ghi nhận thành công.' 
            : `Giao dịch thanh toán chưa hoàn tất (Mã phản hồi VNPay: ${vnpCode || '99'}). Quý khách có thể chuyển khoản trực tiếp hoặc chọn hình thức COD.`
          }
        </p>

        <div style="display:inline-flex; align-items:center; gap:10px; background:#FAF9F5; border:1px solid #D8C3A5; padding:8px 18px; border-radius:8px; margin-bottom:20px;">
          <span style="font-size:0.9rem; color:#5E665E;">Mã đơn hàng:</span>
          <strong style="font-size:1.15rem; color:#B8860B;">${vnpOrder}</strong>
        </div>

        ${vnpTxn ? `
          <div style="font-size:0.85rem; color:#777; margin-bottom:24px;">
            Mã giao dịch ngân hàng: <strong>${vnpTxn}</strong>
          </div>
        ` : ''}

        <div style="display:flex; justify-content:center; gap:14px;">
          <button class="btn btn-outline" onclick="window.orderLookup.openWithCode('${vnpOrder}')">
            🔍 Tra Cứu Đơn Hàng
          </button>
          <button class="btn btn-primary" onclick="document.getElementById('order-result-modal').classList.remove('active'); document.body.style.overflow='';">
            Trang Chủ
          </button>
        </div>
      </div>
    `;

    this.resultModal.classList.add('active');
    document.body.style.overflow = 'hidden';
  }

  copyText(text, btn) {
    if (!navigator.clipboard) {
      const textarea = document.createElement('textarea');
      textarea.value = text;
      document.body.appendChild(textarea);
      textarea.select();
      document.execCommand('copy');
      document.body.removeChild(textarea);
    } else {
      navigator.clipboard.writeText(text);
    }

    if (btn) {
      const original = btn.textContent;
      btn.textContent = 'Đã chép!';
      btn.classList.add('copied');
      setTimeout(() => {
        btn.textContent = original;
        btn.classList.remove('copied');
      }, 1800);
    }
  }
}

// Global instance
window.checkout = new CheckoutManager();
