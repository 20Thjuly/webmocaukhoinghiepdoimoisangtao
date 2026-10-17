/**
 * ECO-ARECA Order Lookup & Tracking Controller
 * Verifies OrderCode + Phone number, displays live order status timeline,
 * item snapshots, and unpaid VietQR instructions.
 */

class OrderLookupManager {
  constructor() {
    this.modal = null;
    this.form = null;
    this.container = null;

    document.addEventListener('DOMContentLoaded', () => this.init());
  }

  init() {
    this.modal = document.getElementById('order-lookup-modal');
    this.closeBtn = document.getElementById('order-lookup-close');
    this.form = document.getElementById('order-lookup-form');
    this.container = document.getElementById('order-lookup-details');

    if (this.closeBtn) {
      this.closeBtn.addEventListener('click', () => this.close());
    }

    if (this.form) {
      this.form.addEventListener('submit', (e) => this.handleLookup(e));
    }

    // Lookup triggers in header or floating widget
    document.querySelectorAll('.open-order-lookup-btn').forEach(btn => {
      btn.addEventListener('click', (e) => {
        e.preventDefault();
        this.open();
      });
    });
  }

  open() {
    if (this.modal) {
      this.modal.classList.add('active');
      document.body.style.overflow = 'hidden';
      if (this.container) this.container.innerHTML = '';
      const codeInput = document.getElementById('lookup-order-code');
      if (codeInput) codeInput.focus();
    }
  }

  openWithCode(orderCode) {
    this.open();
    const codeInput = document.getElementById('lookup-order-code');
    if (codeInput) {
      codeInput.value = orderCode;
      const phoneInput = document.getElementById('lookup-phone');
      if (phoneInput) phoneInput.focus();
    }
  }

  close() {
    if (this.modal) {
      this.modal.classList.remove('active');
      document.body.style.overflow = '';
    }
  }

  async handleLookup(e) {
    e.preventDefault();

    const form = e.target;
    const code = form.lookup_order_code.value.trim();
    const phone = form.lookup_phone.value.trim();

    if (!code || !phone) {
      alert("Vui lòng nhập cả Mã đơn hàng và Số điện thoại đặt hàng!");
      return;
    }

    const submitBtn = form.querySelector('button[type="submit"]');
    if (submitBtn) {
      submitBtn.disabled = true;
      submitBtn.innerHTML = `<span>⏳</span> Đang tra cứu...`;
    }

    try {
      const res = await fetch('/api/order/lookup', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          orderCode: code,
          phoneNumber: phone
        })
      });

      const data = await res.json();

      if (!res.ok || !data.success) {
        alert(data.message || "Không tìm thấy thông tin đơn hàng phù hợp.");
        return;
      }

      this.renderOrderDetails(data.order, data.history, data.bankTransferInfo);

    } catch (err) {
      console.error("Order lookup failed", err);
      alert("Đã xảy ra lỗi trong quá trình tra cứu. Quý khách vui lòng thử lại sau.");
    } finally {
      if (submitBtn) {
        submitBtn.disabled = false;
        submitBtn.innerHTML = `Tra Cứu Đơn Hàng`;
      }
    }
  }

  renderOrderDetails(order, history, bankInfo) {
    if (!this.container) return;

    // Stepper progress index
    // 0: PendingConfirmation, 1: Confirmed, 2: Processing, 3: Shipping, 4: Completed, 5: Cancelled
    const statusMap = {
      'PendingConfirmation': { index: 0, text: 'Chờ xác nhận', class: 'pending' },
      'Confirmed': { index: 1, text: 'Đã xác nhận', class: 'confirmed' },
      'Processing': { index: 2, text: 'Đang chuẩn bị', class: 'processing' },
      'Shipping': { index: 3, text: 'Đang giao hàng', class: 'shipping' },
      'Completed': { index: 4, text: 'Hoàn thành', class: 'completed' },
      'Cancelled': { index: -1, text: 'Đã hủy', class: 'cancelled' }
    };

    const curStatus = statusMap[order.orderStatus] || statusMap['PendingConfirmation'];
    const steps = [
      'Chờ xác nhận',
      'Đã xác nhận',
      'Đang chuẩn bị',
      'Đang giao hàng',
      'Hoàn thành'
    ];

    let stepperHTML = '';
    if (order.orderStatus === 'Cancelled') {
      stepperHTML = `
        <div style="background:#FFEBEE; color:#C62828; padding:12px 18px; border-radius:8px; margin:20px 0; font-weight:700; text-align:center;">
          ❌ Đơn hàng này đã bị hủy. Nếu có thắc mắc, vui lòng liên hệ hotline 0388.750.916.
        </div>
      `;
    } else {
      const progressPercent = Math.max(0, Math.min(100, (curStatus.index / (steps.length - 1)) * 100));
      stepperHTML = `
        <div class="order-stepper">
          <div class="stepper-progress-line" style="width: calc(${progressPercent}% - 40px);"></div>
          ${steps.map((st, idx) => {
            let cls = '';
            if (idx < curStatus.index) cls = 'completed';
            else if (idx === curStatus.index) cls = 'active';

            return `
              <div class="stepper-step ${cls}">
                <div class="stepper-circle">
                  ${idx < curStatus.index ? '✓' : (idx + 1)}
                </div>
                <div class="stepper-title">${st}</div>
              </div>
            `;
          }).join('')}
        </div>
      `;
    }

    // Payment badge
    const paymentStatusBadge = order.paymentStatus === 'Paid'
      ? `<span class="status-pill paid">✓ Đã thanh toán</span>`
      : `<span class="status-pill pending">⏳ Chờ thanh toán</span>`;

    // Unpaid VietQR section
    let unpaidQrHTML = '';
    if (bankInfo && order.paymentStatus !== 'Paid') {
      unpaidQrHTML = `
        <div style="margin-top:24px; padding:20px; background:#FFFDE7; border:1px solid #FFF59D; border-radius:12px;">
          <h4 style="color:#B8860B; margin-bottom:12px; display:flex; align-items:center; gap:8px;">
            <span>🏦</span> Thông Tin Chuyển Khoản Thanh Toán
          </h4>
          <div style="display:grid; grid-template-columns:160px 1fr; gap:18px; align-items:center;">
            <img src="${bankInfo.qrCodeUrl}" alt="VietQR" style="width:100%; border-radius:8px; border:1px solid #DDD;">
            <div style="font-size:0.86rem; color:#444;">
              <p style="margin-bottom:6px;"><strong>Ngân hàng:</strong> ${bankInfo.bankName}</p>
              <p style="margin-bottom:6px;"><strong>Số tài khoản:</strong> <span style="font-weight:700; color:#1E3B2A;">${bankInfo.accountNumber}</span></p>
              <p style="margin-bottom:6px;"><strong>Chủ tài khoản:</strong> ${bankInfo.accountName}</p>
              <p style="margin-bottom:6px;"><strong>Số tiền:</strong> <span style="font-weight:800; color:#C62828;">${formatCurrency(bankInfo.amount)}</span></p>
              <p style="margin-bottom:0;"><strong>Cú pháp:</strong> <span style="background:#FFF9C4; padding:2px 8px; border-radius:4px; font-weight:700; color:#C62828;">${bankInfo.transferContent}</span></p>
            </div>
          </div>
        </div>
      `;
    }

    this.container.innerHTML = `
      <div style="border-top:1px solid var(--color-border); padding-top:20px; margin-top:20px;">
        <div style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:10px; margin-bottom:14px;">
          <div>
            <span style="font-size:0.85rem; color:#777;">Mã đơn hàng:</span>
            <strong style="font-size:1.2rem; color:#1E3B2A; margin-left:6px;">${order.orderCode}</strong>
          </div>
          <div style="display:flex; gap:8px; align-items:center;">
            <span class="status-pill ${curStatus.class}">${curStatus.text}</span>
            ${paymentStatusBadge}
          </div>
        </div>

        ${stepperHTML}

        <div style="display:grid; grid-template-columns:1fr 1fr; gap:20px; background:#FDFBF7; padding:18px; border-radius:10px; border:1px solid #ECE7DD; margin-bottom:20px;">
          <div>
            <h4 style="font-size:0.9rem; color:#1E3B2A; margin-bottom:8px;">Thông Tin Nhận Hàng</h4>
            <div style="font-size:0.85rem; color:#555; line-height:1.6;">
              <p><strong>Người nhận:</strong> ${order.customerName}</p>
              <p><strong>Số điện thoại:</strong> ${order.phoneNumber}</p>
              <p><strong>Địa chỉ:</strong> ${order.fullShippingAddress}</p>
              ${order.customerNote ? `<p><strong>Ghi chú:</strong> ${order.customerNote}</p>` : ''}
            </div>
          </div>
          <div>
            <h4 style="font-size:0.9rem; color:#1E3B2A; margin-bottom:8px;">Hình Thức & Vận Chuyển</h4>
            <div style="font-size:0.85rem; color:#555; line-height:1.6;">
              <p><strong>Phương thức TT:</strong> ${this.formatPaymentMethod(order.paymentMethod)}</p>
              <p><strong>Vận chuyển:</strong> ${order.shippingMethod === 1 ? 'Nhận tại Xưởng Huế' : 'Giao tận nơi toàn quốc'}</p>
              <p><strong>Ngày đặt hàng:</strong> ${new Date(order.createdAt).toLocaleString('vi-VN')}</p>
            </div>
          </div>
        </div>

        <h4 style="font-size:0.95rem; color:#1E3B2A; margin-bottom:12px;">Sản Phẩm Đã Đặt:</h4>
        <div style="display:flex; flex-direction:column; gap:10px; margin-bottom:18px;">
          ${order.items.map(it => `
            <div style="display:flex; justify-content:space-between; align-items:center; padding:10px 14px; background:#fff; border:1px solid #EEE; border-radius:8px;">
              <div style="display:flex; align-items:center; gap:12px;">
                <img src="${it.imageSnapshot}" alt="${it.productNameSnapshot}" style="width:44px; height:44px; object-fit:cover; border-radius:6px; border:1px solid #eee;">
                <div>
                  <div style="font-weight:700; font-size:0.88rem; color:#1E3B2A;">${it.productNameSnapshot}</div>
                  <div style="font-size:0.78rem; color:#777;">${it.variantSnapshot} × ${it.quantity}</div>
                </div>
              </div>
              <div style="font-weight:700; color:#1E3B2A; font-size:0.9rem;">
                ${formatCurrency(it.unitPriceSnapshot * it.quantity)}
              </div>
            </div>
          `).join('')}
        </div>

        <div style="background:#F9F9F9; padding:14px 18px; border-radius:8px; font-size:0.88rem;">
          <div style="display:flex; justify-content:space-between; margin-bottom:6px;">
            <span>Tạm tính:</span>
            <span>${formatCurrency(order.subtotal)}</span>
          </div>
          ${order.discountAmount > 0 ? `
            <div style="display:flex; justify-content:space-between; margin-bottom:6px; color:#2E7D32;">
              <span>Giảm giá (${order.couponCode}):</span>
              <span>-${formatCurrency(order.discountAmount)}</span>
            </div>
          ` : ''}
          <div style="display:flex; justify-content:space-between; margin-bottom:8px;">
            <span>Phí vận chuyển:</span>
            <span>${order.shippingFee === 0 ? 'Miễn phí' : formatCurrency(order.shippingFee)}</span>
          </div>
          <div style="display:flex; justify-content:space-between; padding-top:8px; border-top:1px dashed #DDD; font-size:1.05rem; font-weight:800; color:#1E3B2A;">
            <span>Tổng thanh toán:</span>
            <span style="color:#C49A45;">${formatCurrency(order.totalAmount)}</span>
          </div>
        </div>

        ${unpaidQrHTML}
      </div>
    `;
  }

  formatPaymentMethod(method) {
    if (method === 0 || method === 'COD') return 'Thanh toán khi nhận hàng (COD)';
    if (method === 1 || method === 'BankTransfer') return 'Chuyển khoản ngân hàng (VietQR)';
    if (method === 2 || method === 'VNPay') return 'Cổng thanh toán VNPay';
    return method;
  }
}

// Global instance
window.orderLookup = new OrderLookupManager();
