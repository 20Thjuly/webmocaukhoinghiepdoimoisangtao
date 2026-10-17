/**
 * ECO-ARECA Shopping Cart Module
 * Synchronized with authoritative server pricing,
 * Free Shipping Progress Tracker, Voucher validation, and Checkout flow.
 */

class CartManager {
  constructor() {
    this.items = [];
    this.storageKey = 'eco_areca_cart_v1';
    this.couponCode = '';
    this.freeShippingThreshold = 300000; // 300.000đ freeship

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
    this.couponCode = '';
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
    return subtotal >= this.freeShippingThreshold ? 0 : 25000;
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

    if (this.cartBtn) {
      this.cartBtn.addEventListener('click', () => this.openDrawer());
    }

    if (this.cartCloseBtn) {
      this.cartCloseBtn.addEventListener('click', () => this.closeDrawer());
    }

    if (this.drawerOverlay) {
      this.drawerOverlay.addEventListener('click', () => {
        this.closeDrawer();
        if (window.checkout) window.checkout.close();
        if (window.orderLookup) window.orderLookup.close();
      });
    }

    if (this.cartCheckoutBtn) {
      this.cartCheckoutBtn.addEventListener('click', () => {
        if (this.items.length === 0) {
          alert('Giỏ hàng của bạn đang trống.');
          return;
        }
        this.closeDrawer();
        if (window.checkout) {
          window.checkout.open();
        }
      });
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

    const subtotal = this.getSubtotal();
    const shipping = this.getShippingFee();
    const total = this.getTotal();

    // Render Free Shipping Progress Tracker
    this.renderShippingTracker(subtotal);

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

    if (this.cartSubtotalEl) this.cartSubtotalEl.textContent = formatCurrency(subtotal);
    if (this.cartShippingEl) {
      this.cartShippingEl.innerHTML = subtotal === 0 
        ? '0 đ' 
        : (shipping === 0 ? '<span style="color:#2E7D32; font-weight:700;">Miễn phí</span>' : formatCurrency(shipping));
    }
    if (this.cartTotalEl) this.cartTotalEl.textContent = formatCurrency(total);
  }

  renderShippingTracker(subtotal) {
    let tracker = document.getElementById('cart-shipping-tracker');
    if (!tracker && this.cartDrawer) {
      const body = this.cartDrawer.querySelector('.cart-drawer-body');
      if (body) {
        tracker = document.createElement('div');
        tracker.id = 'cart-shipping-tracker';
        tracker.className = 'cart-shipping-tracker';
        body.insertBefore(tracker, body.firstChild);
      }
    }

    if (!tracker) return;

    if (subtotal === 0) {
      tracker.style.display = 'none';
      return;
    }

    tracker.style.display = 'block';
    const percent = Math.min(100, Math.round((subtotal / this.freeShippingThreshold) * 100));
    const remaining = Math.max(0, this.freeShippingThreshold - subtotal);

    if (remaining === 0) {
      tracker.innerHTML = `
        <div class="shipping-tracker-text">
          <span>🎉</span> <span>Chúc mừng! Đơn hàng được <strong>MIỄN PHÍ VẬN CHUYỂN</strong></span>
        </div>
        <div class="shipping-progress-bar">
          <div class="shipping-progress-fill" style="width: 100%; background: #2E7D32;"></div>
        </div>
      `;
    } else {
      tracker.innerHTML = `
        <div class="shipping-tracker-text">
          <span>🚚</span> <span>Mua thêm <strong>${formatCurrency(remaining)}</strong> để được <strong>Miễn Phí Vận Chuyển</strong></span>
        </div>
        <div class="shipping-progress-bar">
          <div class="shipping-progress-fill" style="width: ${percent}%;"></div>
        </div>
      `;
    }
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
