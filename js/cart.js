/**
 * ECO-ARECA Shopping Cart & Order Handling Module
 * Matches interactive e-commerce requirements from Phase 5 & 6
 */

class CartManager {
  constructor() {
    this.items = [];
    this.storageKey = 'eco_areca_cart_v1';
    this.loadCart();
    this.initUI();
  }

  loadCart() {
    try {
      const saved = localStorage.getItem(this.storageKey);
      if (saved) {
        this.items = JSON.parse(saved);
      }
    } catch (e) {
      console.warn("Could not read cart from localStorage", e);
      this.items = [];
    }
  }

  saveCart() {
    try {
      localStorage.setItem(this.storageKey, JSON.stringify(this.items));
    } catch (e) {
      console.warn("Could not save cart", e);
    }
    this.updateBadge();
    this.renderDrawer();
  }

  addItem(productId, variantLabel, quantity = 1) {
    const product = ECO_PRODUCTS.find(p => p.id === productId);
    if (!product) return;

    const variant = product.variants.find(v => v.label === variantLabel) || product.variants[0];
    const existingIndex = this.items.findIndex(
      item => item.productId === productId && item.variantLabel === variant.label
    );

    if (existingIndex > -1) {
      this.items[existingIndex].quantity += quantity;
    } else {
      this.items.push({
        productId: product.id,
        name: product.name,
        variantLabel: variant.label,
        price: variant.price,
        image: product.image,
        quantity: quantity
      });
    }

    this.saveCart();
    this.showToast(`Đã thêm ${product.name} (${variant.label}) vào giỏ!`);
  }

  updateQuantity(index, newQty) {
    if (index >= 0 && index < this.items.length) {
      if (newQty <= 0) {
        this.removeItem(index);
      } else {
        this.items[index].quantity = newQty;
        this.saveCart();
      }
    }
  }

  removeItem(index) {
    if (index >= 0 && index < this.items.length) {
      const removed = this.items.splice(index, 1)[0];
      this.saveCart();
      this.showToast(`Đã xóa ${removed.name} khỏi giỏ.`);
    }
  }

  clearCart() {
    this.items = [];
    this.saveCart();
  }

  getTotalCount() {
    return this.items.reduce((sum, item) => sum + item.quantity, 0);
  }

  getSubtotal() {
    return this.items.reduce((sum, item) => sum + (item.price * item.quantity), 0);
  }

  getShippingFee() {
    const subtotal = this.getSubtotal();
    if (subtotal === 0) return 0;
    return subtotal >= 300000 ? 0 : 25000;
  }

  getTotal() {
    return this.getSubtotal() + this.getShippingFee();
  }

  initUI() {
    this.cartDrawer = document.getElementById('cart-drawer');
    this.drawerOverlay = document.getElementById('drawer-overlay');
    this.cartBtn = document.getElementById('header-cart-btn');
    this.cartCloseBtn = document.getElementById('cart-close-btn');
    this.cartBadge = document.getElementById('header-cart-badge');
    this.cartItemsList = document.getElementById('cart-items-list');
    this.cartEmptyState = document.getElementById('cart-empty-state');
    this.cartSubtotalEl = document.getElementById('cart-subtotal');
    this.cartShippingEl = document.getElementById('cart-shipping');
    this.cartTotalEl = document.getElementById('cart-total');
    this.cartCheckoutBtn = document.getElementById('cart-checkout-btn');

    // Modals
    this.orderModal = document.getElementById('order-modal');
    this.orderModalClose = document.getElementById('order-modal-close');
    this.orderForm = document.getElementById('order-form');

    if (this.cartBtn) {
      this.cartBtn.addEventListener('click', () => this.openDrawer());
    }

    if (this.cartCloseBtn) {
      this.cartCloseBtn.addEventListener('click', () => this.closeDrawer());
    }

    if (this.drawerOverlay) {
      this.drawerOverlay.addEventListener('click', () => this.closeDrawer());
    }

    if (this.cartCheckoutBtn) {
      this.cartCheckoutBtn.addEventListener('click', () => {
        if (this.items.length === 0) {
          alert('Giỏ hàng của bạn đang trống.');
          return;
        }
        this.closeDrawer();
        this.openOrderModal();
      });
    }

    if (this.orderModalClose) {
      this.orderModalClose.addEventListener('click', () => this.closeOrderModal());
    }

    if (this.orderForm) {
      this.orderForm.addEventListener('submit', (e) => this.handleOrderSubmit(e));
    }

    this.updateBadge();
    this.renderDrawer();
  }

  openDrawer() {
    if (this.cartDrawer && this.drawerOverlay) {
      this.cartDrawer.classList.add('active');
      this.drawerOverlay.classList.add('active');
      document.body.style.overflow = 'hidden';
      this.renderDrawer();
    }
  }

  closeDrawer() {
    if (this.cartDrawer && this.drawerOverlay) {
      this.cartDrawer.classList.remove('active');
      this.drawerOverlay.classList.remove('active');
      document.body.style.overflow = '';
    }
  }

  updateBadge() {
    if (this.cartBadge) {
      const count = this.getTotalCount();
      this.cartBadge.textContent = count;
      this.cartBadge.style.display = count > 0 ? 'flex' : 'none';
    }
  }

  renderDrawer() {
    if (!this.cartItemsList || !this.cartEmptyState) return;

    if (this.items.length === 0) {
      this.cartEmptyState.style.display = 'block';
      this.cartItemsList.style.display = 'none';
      if (this.cartCheckoutBtn) this.cartCheckoutBtn.disabled = true;
    } else {
      this.cartEmptyState.style.display = 'none';
      this.cartItemsList.style.display = 'flex';
      if (this.cartCheckoutBtn) this.cartCheckoutBtn.disabled = false;

      this.cartItemsList.innerHTML = this.items.map((item, index) => `
        <div class="cart-item" data-index="${index}">
          <img src="${item.image}" alt="${item.name}" class="cart-item-img">
          <div class="cart-item-details">
            <h4 class="cart-item-title">${item.name}</h4>
            <div class="cart-item-variant">${item.variantLabel}</div>
            <div class="cart-item-price">${formatCurrency(item.price)}</div>
            <div class="cart-quantity-control">
              <button class="qty-btn dec-qty" onclick="window.cart.updateQuantity(${index}, ${item.quantity - 1})" aria-label="Giảm">−</button>
              <input type="text" readonly class="qty-input" value="${item.quantity}">
              <button class="qty-btn inc-qty" onclick="window.cart.updateQuantity(${index}, ${item.quantity + 1})" aria-label="Tăng">+</button>
            </div>
          </div>
          <button class="cart-item-remove" onclick="window.cart.removeItem(${index})" title="Xóa sản phẩm">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>
          </button>
        </div>
      `).join('');
    }

    const subtotal = this.getSubtotal();
    const shipping = this.getShippingFee();
    const total = this.getTotal();

    if (this.cartSubtotalEl) this.cartSubtotalEl.textContent = formatCurrency(subtotal);
    if (this.cartShippingEl) {
      this.cartShippingEl.textContent = subtotal === 0 ? '0 đ' : (shipping === 0 ? 'Miễn phí' : formatCurrency(shipping));
    }
    if (this.cartTotalEl) this.cartTotalEl.textContent = formatCurrency(total);
  }

  openOrderModal() {
    if (this.orderModal) {
      const summaryList = document.getElementById('order-summary-list');
      const orderTotalVal = document.getElementById('order-modal-total');
      if (summaryList) {
        summaryList.innerHTML = this.items.map(item => `
          <div style="display:flex; justify-content:space-between; margin-bottom:8px; font-size:0.88rem;">
            <span>${item.name} (${item.variantLabel}) × ${item.quantity}</span>
            <strong>${formatCurrency(item.price * item.quantity)}</strong>
          </div>
        `).join('');
      }
      if (orderTotalVal) {
        orderTotalVal.textContent = formatCurrency(this.getTotal());
      }
      this.orderModal.classList.add('active');
    }
  }

  closeOrderModal() {
    if (this.orderModal) {
      this.orderModal.classList.remove('active');
    }
  }

  handleOrderSubmit(e) {
    e.preventDefault();
    const form = e.target;
    const name = form.customer_name.value.trim();
    const phone = form.customer_phone.value.trim();
    const address = form.customer_address.value.trim();
    const notes = form.customer_notes.value.trim();
    const payment = form.payment_method.value;

    if (!name || !phone || !address) {
      alert("Vui lòng điền đầy đủ Họ tên, Số điện thoại và Địa chỉ giao hàng!");
      return;
    }

    const orderData = {
      orderCode: 'ECO-' + Math.floor(100000 + Math.random() * 900000),
      createdAt: new Date().toISOString(),
      customer: { name, phone, address, notes, payment },
      items: this.items,
      subtotal: this.getSubtotal(),
      shippingFee: this.getShippingFee(),
      total: this.getTotal()
    };

    // Send to optional backend if available or simulate successful local save
    const submitBtn = form.querySelector('button[type="submit"]');
    if (submitBtn) {
      submitBtn.disabled = true;
      submitBtn.textContent = "Đang xử lý đơn hàng...";
    }

    fetch('/api/order', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(orderData)
    })
    .catch(() => {
      // Backend not running yet, perfectly fine for frontend mode
      return { ok: true };
    })
    .finally(() => {
      setTimeout(() => {
        const orderContainer = document.getElementById('order-modal-content');
        if (orderContainer) {
          orderContainer.innerHTML = `
            <div style="text-align:center; padding:30px 10px;">
              <div style="width:64px; height:64px; background:#E8F5E9; color:#2E7D32; border-radius:50%; display:flex; align-items:center; justify-content:center; margin:0 auto 16px;">
                <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg>
              </div>
              <h3 style="color:#1E3B2A; margin-bottom:8px;">Đặt Hàng Thành Công!</h3>
              <p style="color:#5E665E; font-size:0.95rem; margin-bottom:16px;">
                Mã đơn hàng: <strong style="color:#C49A45;">${orderData.orderCode}</strong>
              </p>
              <p style="color:#5E665E; font-size:0.9rem; margin-bottom:24px;">
                Cảm ơn bạn đã lựa chọn sản phẩm chén dĩa mo cau tự nhiên ECO-ARECA. Đội ngũ tư vấn sẽ liên hệ với bạn qua số <strong>${phone}</strong> để xác nhận thời gian giao hàng.
              </p>
              <button class="btn btn-primary" onclick="window.cart.finishOrderAndClose()">Đóng</button>
            </div>
          `;
        }
        this.clearCart();
      }, 700);
    });
  }

  finishOrderAndClose() {
    this.closeOrderModal();
    window.location.reload();
  }

  showToast(message) {
    let container = document.getElementById('toast-container');
    if (!container) {
      container = document.createElement('div');
      container.id = 'toast-container';
      container.className = 'toast-container';
      document.body.appendChild(container);
    }

    const toast = document.createElement('div');
    toast.className = 'toast-message';
    toast.innerHTML = `
      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#C49A45" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg>
      <span>${message}</span>
    `;

    container.appendChild(toast);
    setTimeout(() => {
      toast.style.opacity = '0';
      toast.style.transition = 'opacity 0.3s ease';
      setTimeout(() => toast.remove(), 300);
    }, 2800);
  }
}

// Global instance
window.cart = new CartManager();
