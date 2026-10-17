/**
 * ECO-ARECA Admin Order Management Controller
 * Handles loading stats, filtering orders, detail inspect modal,
 * status updates, and manual bank transfer payment confirmation.
 */

class AdminManager {
  constructor() {
    this.adminKey = 'EcoAreca@Admin2026';
    document.addEventListener('DOMContentLoaded', () => this.init());
  }

  init() {
    this.loadStats();
    this.loadOrders();
  }

  getHeaders() {
    return {
      'Content-Type': 'application/json',
      'X-Admin-Key': this.adminKey
    };
  }

  async loadStats() {
    try {
      const res = await fetch('/api/admin/stats', {
        headers: this.getHeaders()
      });

      if (res.ok) {
        const stats = await res.json();
        document.getElementById('stat-total-orders').textContent = stats.totalOrders;
        document.getElementById('stat-pending-orders').textContent = stats.pendingOrders;
        document.getElementById('stat-pending-transfers').textContent = stats.pendingTransfers;
        document.getElementById('stat-total-revenue').textContent = formatCurrency(stats.totalRevenue);
        document.getElementById('stat-today-orders').textContent = stats.todayOrders;
        return;
      }
    } catch (e) {
      console.warn("Server admin stats not reachable, calculating from local storage", e);
    }

    // Local stats fallback
    try {
      const localOrders = JSON.parse(localStorage.getItem('ecoareca_local_orders') || '[]');
      const total = localOrders.length;
      const pending = localOrders.filter(o => o.orderStatus === 'PendingConfirmation').length;
      const pendingTransfers = localOrders.filter(o => o.paymentMethod === 'BankTransfer' && o.paymentStatus === 'Pending').length;
      const revenue = localOrders.filter(o => o.paymentStatus === 'Paid').reduce((sum, o) => sum + (o.totalAmount || 0), 0);
      document.getElementById('stat-total-orders').textContent = total;
      document.getElementById('stat-pending-orders').textContent = pending;
      document.getElementById('stat-pending-transfers').textContent = pendingTransfers;
      document.getElementById('stat-total-revenue').textContent = formatCurrency(revenue);
      document.getElementById('stat-today-orders').textContent = total;
    } catch (e) {}
  }

  async loadOrders() {
    const tbody = document.getElementById('admin-orders-tbody');
    if (!tbody) return;

    const search = document.getElementById('admin-search-input')?.value.trim().toLowerCase() || '';
    const status = document.getElementById('filter-order-status')?.value || '';
    const paymentStatus = document.getElementById('filter-payment-status')?.value || '';
    const paymentMethod = document.getElementById('filter-payment-method')?.value || '';

    const params = new URLSearchParams();
    if (search) params.set('search', search);
    if (status) params.set('status', status);
    if (paymentStatus) params.set('paymentStatus', paymentStatus);
    if (paymentMethod) params.set('paymentMethod', paymentMethod);

    try {
      const res = await fetch(`/api/admin/orders?${params.toString()}`, {
        headers: this.getHeaders()
      });

      if (res.ok) {
        const orders = await res.json();

        if (orders.length === 0) {
          tbody.innerHTML = `<tr><td colspan="9" style="text-align:center; padding:30px; color:#888;">Không tìm thấy đơn hàng nào phù hợp với điều kiện lọc.</td></tr>`;
          return;
        }

        this.renderOrdersTable(orders, tbody);
        return;
      }
    } catch (e) {
      console.warn("Server admin orders not reachable, reading local storage", e);
    }

    // Local orders fallback
    try {
      let localOrders = JSON.parse(localStorage.getItem('ecoareca_local_orders') || '[]');
      if (search) {
        localOrders = localOrders.filter(o => 
          (o.orderCode && o.orderCode.toLowerCase().includes(search)) ||
          (o.phoneNumber && o.phoneNumber.includes(search)) ||
          (o.customerName && o.customerName.toLowerCase().includes(search))
        );
      }
      if (status) localOrders = localOrders.filter(o => o.orderStatus === status);
      if (paymentStatus) localOrders = localOrders.filter(o => o.paymentStatus === paymentStatus);
      if (paymentMethod) localOrders = localOrders.filter(o => o.paymentMethod === paymentMethod);

      if (localOrders.length === 0) {
        tbody.innerHTML = `<tr><td colspan="9" style="text-align:center; padding:30px; color:#888;">Chưa có đơn hàng nào trên thiết bị này.</td></tr>`;
        return;
      }

      this.renderOrdersTable(localOrders, tbody);
    } catch (e) {
      tbody.innerHTML = `<tr><td colspan="9" style="text-align:center; color:#C62828;">Không thể đọc danh sách đơn hàng.</td></tr>`;
    }
  }

  renderOrdersTable(orders, tbody) {
    tbody.innerHTML = orders.map(o => {
      const statusMap = {
        'PendingConfirmation': { text: 'Chờ xác nhận', class: 'pending' },
        'Confirmed': { text: 'Đã xác nhận', class: 'confirmed' },
        'Processing': { text: 'Đang chuẩn bị', class: 'processing' },
        'Shipping': { text: 'Đang giao', class: 'shipping' },
        'Completed': { text: 'Hoàn thành', class: 'completed' },
        'Cancelled': { text: 'Đã hủy', class: 'cancelled' }
      };

      const paymentStatusMap = {
        'Pending': { text: 'Chờ thanh toán', class: 'pending' },
        'Paid': { text: 'Đã thanh toán', class: 'paid' },
        'Failed': { text: 'Thất bại', class: 'failed' },
        'Cancelled': { text: 'Đã hủy', class: 'cancelled' }
      };

      const orderSt = statusMap[o.orderStatus] || { text: o.orderStatus, class: 'pending' };
      const paySt = paymentStatusMap[o.paymentStatus] || { text: o.paymentStatus, class: 'pending' };

      const itemsSummary = (o.items || []).map(it => `${it.productNameSnapshot} (${it.quantity})`).join(', ');

      return `
        <tr>
          <td>
            <strong style="color:#1E3B2A; cursor:pointer;" onclick="window.admin.viewOrderDetail('${o.id}')">
              ${o.orderCode}
            </strong>
          </td>
          <td>
            <div style="font-weight:700;">${o.customerName}</div>
            <div style="font-size:0.78rem; color:#666;">${o.phoneNumber}</div>
          </td>
          <td>
            <div style="font-size:0.82rem;">${new Date(o.createdAt).toLocaleDateString('vi-VN')}</div>
            <div style="font-size:0.75rem; color:#888;">${new Date(o.createdAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}</div>
          </td>
          <td style="max-width:220px; white-space:nowrap; overflow:hidden; text-overflow:ellipsis;" title="${itemsSummary}">
            <span style="font-weight:600;">${(o.items || []).length} món:</span> ${itemsSummary}
          </td>
          <td>
            <strong style="color:#B8860B;">${formatCurrency(o.totalAmount)}</strong>
          </td>
          <td>
            <span style="font-size:0.8rem; font-weight:600;">${this.formatMethod(o.paymentMethod)}</span>
          </td>
          <td>
            <span class="status-pill ${paySt.class}">${paySt.text}</span>
          </td>
          <td>
            <span class="status-pill ${orderSt.class}">${orderSt.text}</span>
          </td>
          <td style="text-align:right; white-space:nowrap;">
            <button class="action-btn-sm view" onclick="window.admin.viewOrderDetail('${o.id}')" title="Xem chi tiết">
              🔍 Xem
            </button>
            ${o.orderStatus === 'PendingConfirmation' ? `
              <button class="action-btn-sm confirm" onclick="window.admin.quickConfirmOrder('${o.id}')" title="Duyệt đơn nhanh">
                ✓ Duyệt
              </button>
            ` : ''}
            ${o.paymentStatus === 'Pending' && o.paymentMethod === 'BankTransfer' ? `
              <button class="action-btn-sm pay" onclick="window.admin.quickConfirmPayment('${o.id}', ${o.totalAmount})" title="Xác nhận đã nhận chuyển khoản">
                🏦 Đã Nhận CK
              </button>
            ` : ''}
          </td>
        </tr>
      `;
    }).join('');
  }

  async viewOrderDetail(id) {
    const modal = document.getElementById('admin-detail-modal');
    const content = document.getElementById('admin-modal-content');
    if (!modal || !content) return;

    content.innerHTML = `<div style="text-align:center; padding:40px; color:#666;">Đang tải chi tiết đơn hàng...</div>`;
    modal.classList.add('active');
    document.body.style.overflow = 'hidden';

    let order, payments, history;
    try {
      const res = await fetch(`/api/admin/orders/${id}`, {
        headers: this.getHeaders()
      });

      if (res.ok) {
        const data = await res.json();
        order = data.order;
        payments = data.payments || [];
        history = data.history || [];
      } else {
        throw new Error("Server response not ok");
      }
    } catch (e) {
      console.warn("Using local order details fallback", e);
      const localOrders = JSON.parse(localStorage.getItem('ecoareca_local_orders') || '[]');
      const found = localOrders.find(o => o.id === id);
      if (found) {
        order = found;
        payments = [];
        history = found.history || [];
      } else {
        content.innerHTML = `<div style="color:red; text-align:center;">Không tìm thấy dữ liệu đơn hàng.</div>`;
        return;
      }
    }

    try {
      document.getElementById('admin-modal-title').textContent = `Chi Tiết Đơn Hàng ${order.orderCode}`;

      content.innerHTML = `
        <div style="display:grid; grid-template-columns:1fr 1fr; gap:20px; margin-bottom:24px; background:#FAFBF9; padding:20px; border-radius:10px; border:1px solid #EAEFEA;">
          <div>
            <h4 style="font-size:0.95rem; color:#1E3B2A; margin-bottom:10px;">Thông Tin Khách Hàng</h4>
            <div style="font-size:0.86rem; color:#444; line-height:1.6;">
              <p><strong>Họ và tên:</strong> ${order.customerName}</p>
              <p><strong>Số điện thoại:</strong> <a href="tel:${order.phoneNumber}" style="color:#2E7D32; font-weight:700;">${order.phoneNumber}</a></p>
              ${order.email ? `<p><strong>Email:</strong> ${order.email}</p>` : ''}
              <p><strong>Địa chỉ:</strong> ${order.fullShippingAddress}</p>
              ${order.customerNote ? `<p><strong>Ghi chú của khách:</strong> ${order.customerNote}</p>` : ''}
            </div>
          </div>

          <div>
            <h4 style="font-size:0.95rem; color:#1E3B2A; margin-bottom:10px;">Thanh Toán & Vận Chuyển</h4>
            <div style="font-size:0.86rem; color:#444; line-height:1.6;">
              <p><strong>Phương thức TT:</strong> ${this.formatMethod(order.paymentMethod)}</p>
              <p><strong>Trạng thái TT:</strong> <span class="status-pill ${order.paymentStatus === 'Paid' ? 'paid' : 'pending'}">${order.paymentStatus}</span></p>
              <p><strong>Hình thức giao:</strong> ${order.shippingMethod === 1 ? 'Khách nhận tại Xưởng Huế' : 'Giao tận nơi toàn quốc'}</p>
              <p><strong>Thời gian tạo:</strong> ${new Date(order.createdAt).toLocaleString('vi-VN')}</p>
            </div>
          </div>
        </div>

        <h4 style="font-size:0.95rem; color:#1E3B2A; margin-bottom:12px;">Danh Sách Sản Phẩm Trong Đơn:</h4>
        <div style="display:flex; flex-direction:column; gap:8px; margin-bottom:20px;">
          ${order.items.map(it => `
            <div style="display:flex; justify-content:space-between; align-items:center; padding:10px 14px; background:#fff; border:1px solid #ECECEC; border-radius:8px;">
              <div style="display:flex; align-items:center; gap:12px;">
                <img src="${it.imageSnapshot}" alt="${it.productNameSnapshot}" style="width:40px; height:40px; object-fit:cover; border-radius:4px;">
                <div>
                  <div style="font-weight:700; font-size:0.88rem; color:#1E3B2A;">${it.productNameSnapshot}</div>
                  <div style="font-size:0.78rem; color:#777;">${it.variantSnapshot} — Đơn giá snapshot: ${formatCurrency(it.unitPriceSnapshot)}</div>
                </div>
              </div>
              <div style="text-align:right;">
                <div style="font-size:0.85rem; color:#666;">Số lượng: <strong>× ${it.quantity}</strong></div>
                <div style="font-weight:700; color:#1E3B2A;">${formatCurrency(it.unitPriceSnapshot * it.quantity)}</div>
              </div>
            </div>
          `).join('')}
        </div>

        <div style="background:#F7F9F7; padding:14px 18px; border-radius:8px; margin-bottom:24px; font-size:0.9rem;">
          <div style="display:flex; justify-content:space-between; margin-bottom:6px;">
            <span>Tạm tính (Subtotal):</span>
            <span>${formatCurrency(order.subtotal)}</span>
          </div>
          ${order.discountAmount > 0 ? `
            <div style="display:flex; justify-content:space-between; margin-bottom:6px; color:#2E7D32;">
              <span>Giảm giá (${order.couponCode}):</span>
              <span>-${formatCurrency(order.discountAmount)}</span>
            </div>
          ` : ''}
          <div style="display:flex; justify-content:space-between; margin-bottom:8px;">
            <span>Phí giao hàng:</span>
            <span>${order.shippingFee === 0 ? 'Miễn phí' : formatCurrency(order.shippingFee)}</span>
          </div>
          <div style="display:flex; justify-content:space-between; padding-top:8px; border-top:1px dashed #DDD; font-size:1.15rem; font-weight:800; color:#1E3B2A;">
            <span>Tổng thanh toán:</span>
            <span style="color:#C49A45;">${formatCurrency(order.totalAmount)}</span>
          </div>
        </div>

        <!-- Action Section: Update Status & Payment -->
        <div style="display:grid; grid-template-columns:1fr 1fr; gap:20px; background:#fff; border:1.5px solid var(--color-border); border-radius:10px; padding:18px; margin-bottom:24px;">
          <div>
            <h4 style="font-size:0.9rem; color:#1E3B2A; margin-bottom:10px;">Cập Nhật Trạng Thái Đơn</h4>
            <div style="display:flex; gap:8px; margin-bottom:10px;">
              <select id="modal-update-status-select" class="form-select" style="font-size:0.85rem;">
                <option value="PendingConfirmation" ${order.orderStatus === 'PendingConfirmation' ? 'selected' : ''}>Chờ xác nhận</option>
                <option value="Confirmed" ${order.orderStatus === 'Confirmed' ? 'selected' : ''}>Đã xác nhận</option>
                <option value="Processing" ${order.orderStatus === 'Processing' ? 'selected' : ''}>Đang chuẩn bị</option>
                <option value="Shipping" ${order.orderStatus === 'Shipping' ? 'selected' : ''}>Đang giao hàng</option>
                <option value="Completed" ${order.orderStatus === 'Completed' ? 'selected' : ''}>Hoàn thành</option>
                <option value="Cancelled" ${order.orderStatus === 'Cancelled' ? 'selected' : ''}>Hủy đơn</option>
              </select>
              <button class="btn btn-primary btn-sm" onclick="window.admin.saveOrderStatus('${order.id}')">
                Lưu
              </button>
            </div>
            <input type="text" id="modal-update-status-note" class="form-control" placeholder="Ghi chú cập nhật trạng thái..." style="font-size:0.8rem;">
          </div>

          <div>
            <h4 style="font-size:0.9rem; color:#1E3B2A; margin-bottom:10px;">Xác Nhận Thanh Toán Thủ Công</h4>
            ${order.paymentStatus === 'Paid' ? `
              <div style="color:#2E7D32; font-weight:700; font-size:0.9rem; padding:10px 0;">
                ✓ Đơn hàng này đã được xác nhận thanh toán thành công!
              </div>
            ` : `
              <div style="display:flex; flex-direction:column; gap:8px;">
                <input type="text" id="modal-confirm-pay-ref" class="form-control" placeholder="Mã giao dịch ngân hàng / Ref..." style="font-size:0.8rem;">
                <button class="btn btn-accent btn-sm" onclick="window.admin.saveConfirmPayment('${order.id}', ${order.totalAmount})">
                  Xác nhận đã nhận đủ ${formatCurrency(order.totalAmount)}
                </button>
              </div>
            `}
          </div>
        </div>

        <!-- Audit Trail / Status History -->
        <h4 style="font-size:0.95rem; color:#1E3B2A; margin-bottom:12px;">Nhật Ký Lịch Sử Trạng Thái (Audit Trail):</h4>
        <div style="background:#FAFBF9; border:1px solid #ECECEC; border-radius:8px; padding:14px; max-height:180px; overflow-y:auto; font-size:0.82rem;">
          ${history.length === 0 ? '<div style="color:#888;">Chưa có lịch sử.</div>' : history.map(h => `
            <div style="display:flex; justify-content:space-between; padding-bottom:8px; margin-bottom:8px; border-bottom:1px dashed #EEE;">
              <div>
                <strong>${h.changedBy}:</strong> ${h.note || `Chuyển trạng thái sang ${h.newStatus}`}
              </div>
              <div style="color:#777; font-size:0.75rem;">
                ${new Date(h.changedAt).toLocaleString('vi-VN')}
              </div>
            </div>
          `).join('')}
        </div>
      `;

    } catch (e) {
      console.error("Error viewing detail", e);
    }
  }

  async quickConfirmOrder(id) {
    if (!confirm("Bạn có chắc chắn muốn duyệt và xác nhận đơn hàng này?")) return;

    let updated = false;
    try {
      const res = await fetch(`/api/admin/orders/${id}/status`, {
        method: 'POST',
        headers: this.getHeaders(),
        body: JSON.stringify({
          newStatus: 1, // Confirmed
          note: "Quản trị viên duyệt đơn nhanh",
          adminName: "Admin"
        })
      });
      if (res.ok) updated = true;
    } catch (e) {
      console.warn("Server update status unavailable, updating local storage", e);
    }

    if (!updated) {
      try {
        const localOrders = JSON.parse(localStorage.getItem('ecoareca_local_orders') || '[]');
        const target = localOrders.find(o => o.id === id);
        if (target) {
          target.orderStatus = 'Confirmed';
          target.history = target.history || [];
          target.history.push({
            fromStatus: 'PendingConfirmation',
            toStatus: 'Confirmed',
            changedAt: new Date().toISOString(),
            changedBy: 'Admin (Duyệt nhanh)'
          });
          localStorage.setItem('ecoareca_local_orders', JSON.stringify(localOrders));
          updated = true;
        }
      } catch (err) {}
    }

    if (updated) {
      this.loadOrders();
      this.loadStats();
    }
  }

  async quickConfirmPayment(id, amount) {
    const txn = prompt(`Nhập mã đối soát ngân hàng cho đơn hàng (Số tiền: ${formatCurrency(amount)}):`, `MB-${Date.now().toString().slice(-6)}`);
    if (!txn) return;

    let confirmed = false;
    try {
      const res = await fetch(`/api/admin/orders/${id}/confirm-payment`, {
        method: 'POST',
        headers: this.getHeaders(),
        body: JSON.stringify({
          amount: amount,
          transactionReference: txn,
          note: "Đối soát chuyển khoản ngân hàng thành công",
          adminName: "Admin"
        })
      });
      if (res.ok) confirmed = true;
    } catch (e) {
      console.warn("Server payment confirm unavailable, updating local storage", e);
    }

    if (!confirmed) {
      try {
        const localOrders = JSON.parse(localStorage.getItem('ecoareca_local_orders') || '[]');
        const target = localOrders.find(o => o.id === id);
        if (target) {
          target.paymentStatus = 'Paid';
          target.history = target.history || [];
          target.history.push({
            fromStatus: 'Pending',
            toStatus: 'Paid',
            changedAt: new Date().toISOString(),
            changedBy: `Admin (Đối soát: ${txn})`
          });
          localStorage.setItem('ecoareca_local_orders', JSON.stringify(localOrders));
          confirmed = true;
        }
      } catch (err) {}
    }

    if (confirmed) {
      alert("Đã xác nhận thanh toán thành công!");
      this.loadOrders();
      this.loadStats();
    }
  }

  async saveOrderStatus(orderId) {
    const select = document.getElementById('modal-update-status-select');
    const noteInput = document.getElementById('modal-update-status-note');
    if (!select) return;

    const statusMap = {
      'PendingConfirmation': 0,
      'Confirmed': 1,
      'Processing': 2,
      'Shipping': 3,
      'Completed': 4,
      'Cancelled': 5
    };

    const newStatusInt = statusMap[select.value] ?? 0;
    const note = noteInput ? noteInput.value.trim() : '';

    let updated = false;
    try {
      const res = await fetch(`/api/admin/orders/${orderId}/status`, {
        method: 'POST',
        headers: this.getHeaders(),
        body: JSON.stringify({
          newStatus: newStatusInt,
          note: note || `Cập nhật trạng thái sang ${select.value}`,
          adminName: "Admin"
        })
      });
      if (res.ok) updated = true;
    } catch (e) {
      console.warn("Server update status unavailable, updating local storage", e);
    }

    if (!updated) {
      try {
        const localOrders = JSON.parse(localStorage.getItem('ecoareca_local_orders') || '[]');
        const target = localOrders.find(o => o.id === orderId);
        if (target) {
          target.orderStatus = select.value;
          target.history = target.history || [];
          target.history.push({
            fromStatus: target.orderStatus,
            toStatus: select.value,
            changedAt: new Date().toISOString(),
            changedBy: 'Admin'
          });
          localStorage.setItem('ecoareca_local_orders', JSON.stringify(localOrders));
          updated = true;
        }
      } catch (err) {}
    }

    if (updated) {
      alert("Đã cập nhật trạng thái đơn hàng!");
      this.viewOrderDetail(orderId);
      this.loadOrders();
      this.loadStats();
    }
  }

  async saveConfirmPayment(orderId, amount) {
    const refInput = document.getElementById('modal-confirm-pay-ref');
    const ref = refInput ? refInput.value.trim() : '';
    const txnRef = ref || `BANK-${Date.now().toString().slice(-6)}`;

    let confirmed = false;
    try {
      const res = await fetch(`/api/admin/orders/${orderId}/confirm-payment`, {
        method: 'POST',
        headers: this.getHeaders(),
        body: JSON.stringify({
          amount: amount,
          transactionReference: txnRef,
          note: "Xác nhận chuyển khoản ngân hàng từ trang chi tiết",
          adminName: "Admin"
        })
      });
      if (res.ok) confirmed = true;
    } catch (e) {
      console.warn("Server payment confirm unavailable, updating local storage", e);
    }

    if (!confirmed) {
      try {
        const localOrders = JSON.parse(localStorage.getItem('ecoareca_local_orders') || '[]');
        const target = localOrders.find(o => o.id === orderId);
        if (target) {
          target.paymentStatus = 'Paid';
          target.history = target.history || [];
          target.history.push({
            fromStatus: 'Pending',
            toStatus: 'Paid',
            changedAt: new Date().toISOString(),
            changedBy: `Admin (${txnRef})`
          });
          localStorage.setItem('ecoareca_local_orders', JSON.stringify(localOrders));
          confirmed = true;
        }
      } catch (err) {}
    }

    if (confirmed) {
      alert("Đã xác nhận thanh toán thành công cho đơn hàng!");
      this.viewOrderDetail(orderId);
      this.loadOrders();
      this.loadStats();
    }
  }

  refreshData() {
    this.loadStats();
    this.loadOrders();
  }

  formatMethod(method) {
    if (method === 0 || method === 'COD') return 'Tiền mặt (COD)';
    if (method === 1 || method === 'BankTransfer') return 'Chuyển khoản (VietQR)';
    if (method === 2 || method === 'VNPay') return 'Cổng VNPay';
    return method;
  }
}

// Global instance
window.admin = new AdminManager();
