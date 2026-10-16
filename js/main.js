/**
 * ECO-ARECA Main Interactive UI Controller
 * Manages product rendering, filtering, quickview, header scrolling, and mobile drawer.
 */

document.addEventListener('DOMContentLoaded', () => {
  initHeader();
  renderProductsGrid('all');
  initProductTabs();
  initQuickViewModal();
  initMobileDrawer();
  initFloatingWidgets();
});

/* Header scroll effect & active section observer */
function initHeader() {
  const header = document.querySelector('.main-header');
  const navLinks = document.querySelectorAll('.nav-link');
  const sections = document.querySelectorAll('section[id], div[id].section');

  window.addEventListener('scroll', () => {
    if (window.scrollY > 40) {
      header.classList.add('scrolled');
    } else {
      header.classList.remove('scrolled');
    }

    // ScrollSpy active link
    let currentId = '';
    const scrollPos = window.scrollY + 120;
    sections.forEach(sec => {
      const top = sec.offsetTop;
      const height = sec.offsetHeight;
      if (scrollPos >= top && scrollPos < top + height) {
        currentId = sec.getAttribute('id');
      }
    });

    navLinks.forEach(link => {
      const href = link.getAttribute('href');
      if (href === `#${currentId}`) {
        link.classList.add('active');
      } else {
        link.classList.remove('active');
      }
    });
  });
}

/* Render Products in Grid with Variant Selectors */
function renderProductsGrid(categoryFilter = 'all') {
  const grid = document.getElementById('products-grid');
  if (!grid) return;

  const filtered = categoryFilter === 'all' 
    ? ECO_PRODUCTS 
    : ECO_PRODUCTS.filter(p => p.category === categoryFilter);

  grid.innerHTML = filtered.map(product => {
    const defaultVariant = product.variants.find(v => v.default) || product.variants[0];

    return `
      <div class="product-card" data-product-id="${product.id}" data-selected-variant="${defaultVariant.label}" data-selected-price="${defaultVariant.price}">
        <div class="product-badge-wrap">
          <span class="badge-tag ${product.badgeClass}">${product.badge}</span>
        </div>

        <div class="product-img-wrapper">
          <img src="${product.image}" alt="${product.name}" loading="lazy">
          <button class="quickview-btn" onclick="openQuickView('${product.id}')">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path><circle cx="12" cy="12" r="3"></circle></svg>
            Xem Nhanh
          </button>
        </div>

        <div class="product-body">
          <div class="product-specs">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect><line x1="3" y1="9" x2="21" y2="9"></line><line x1="9" y1="21" x2="9" y2="9"></line></svg>
            <span>${product.dimensions}</span>
          </div>

          <h3 class="product-title">${product.name}</h3>
          <p class="product-desc">${product.shortDesc}</p>

          <div class="product-variants-label">Chọn quy cách đóng gói:</div>
          <div class="product-variants">
            ${product.variants.map((v, i) => `
              <button 
                type="button" 
                class="variant-btn ${v.label === defaultVariant.label ? 'active' : ''}" 
                onclick="selectProductVariant('${product.id}', '${v.label}', ${v.price}, this)"
              >
                ${v.label}
              </button>
            `).join('')}
          </div>

          <div class="product-price-row">
            <div>
              <span class="price-main" id="price-display-${product.id}">${formatCurrency(defaultVariant.price)}</span>
            </div>
            <span class="price-note">Giá tham khảo demo</span>
          </div>

          <div class="product-actions">
            <button class="btn btn-outline btn-sm" onclick="addToCartCurrent('${product.id}')">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="9" cy="21" r="1"></circle><circle cx="20" cy="21" r="1"></circle><path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"></path></svg>
              Thêm Vào Giỏ
            </button>
            <button class="btn btn-primary btn-sm" onclick="buyNowCurrent('${product.id}')">
              Mua Ngay
            </button>
          </div>

          <a href="#quote-form" class="b2b-link" onclick="prefillQuoteForProduct('${product.name}')">
            🤝 Liên hệ giá sỉ cho đối tác F&B / Nhà phân phối
          </a>
        </div>
      </div>
    `;
  }).join('');
}

/* Variant Switching */
window.selectProductVariant = function(productId, variantLabel, price, btn) {
  const card = document.querySelector(`.product-card[data-product-id="${productId}"]`);
  if (!card) return;

  card.querySelectorAll('.variant-btn').forEach(b => b.classList.remove('active'));
  btn.classList.add('active');

  card.setAttribute('data-selected-variant', variantLabel);
  card.setAttribute('data-selected-price', price);

  const priceEl = document.getElementById(`price-display-${productId}`);
  if (priceEl) priceEl.textContent = formatCurrency(price);
};

/* Add to Cart from Card */
window.addToCartCurrent = function(productId) {
  const card = document.querySelector(`.product-card[data-product-id="${productId}"]`);
  if (!card) return;

  const variantLabel = card.getAttribute('data-selected-variant');
  window.cart.addItem(productId, variantLabel, 1);
};

/* Buy Now from Card */
window.buyNowCurrent = function(productId) {
  const card = document.querySelector(`.product-card[data-product-id="${productId}"]`);
  if (!card) return;

  const variantLabel = card.getAttribute('data-selected-variant');
  window.cart.addItem(productId, variantLabel, 1);
  window.cart.openDrawer();
};

/* Category Tabs */
function initProductTabs() {
  const tabs = document.querySelectorAll('.product-tab');
  tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      tabs.forEach(t => t.classList.remove('active'));
      tab.classList.add('active');
      const category = tab.getAttribute('data-category');
      renderProductsGrid(category);
    });
  });
}

/* QuickView Modal */
function initQuickViewModal() {
  const modal = document.getElementById('quickview-modal');
  const closeBtn = document.getElementById('quickview-close-btn');

  if (closeBtn && modal) {
    closeBtn.addEventListener('click', () => {
      modal.classList.remove('active');
    });
  }

  if (modal) {
    modal.addEventListener('click', (e) => {
      if (e.target === modal) {
        modal.classList.remove('active');
      }
    });
  }
}

window.openQuickView = function(productId) {
  const product = ECO_PRODUCTS.find(p => p.id === productId);
  if (!product) return;

  const modal = document.getElementById('quickview-modal');
  const content = document.getElementById('quickview-modal-content');
  if (!modal || !content) return;

  const defaultVariant = product.variants.find(v => v.default) || product.variants[0];

  content.innerHTML = `
    <div style="display:grid; grid-template-columns: 1fr 1fr; gap:28px; align-items:start;">
      <div style="border-radius:12px; overflow:hidden; background:#F8F5EE;">
        <img src="${product.image}" alt="${product.name}" style="width:100%; height:320px; object-fit:cover;">
      </div>
      <div>
        <div style="font-size:0.8rem; color:#315C3B; font-weight:700; margin-bottom:6px;">${product.dimensions}</div>
        <h3 style="font-size:1.45rem; color:#1E3B2A; margin-bottom:12px;">${product.name}</h3>
        <p style="font-size:0.9rem; color:#5E665E; line-height:1.6; margin-bottom:18px;">${product.fullDesc}</p>
        
        <div style="margin-bottom:14px;">
          <label style="display:block; font-size:0.85rem; font-weight:700; margin-bottom:8px;">Quy cách đóng gói:</label>
          <div style="display:flex; flex-wrap:wrap; gap:8px;">
            ${product.variants.map((v, i) => `
              <button 
                type="button" 
                class="variant-btn ${i === 0 ? 'active' : ''}" 
                id="qv-var-${i}"
                onclick="selectQuickViewVariant('${product.id}', '${v.label}', ${v.price}, this)"
              >
                ${v.label}
              </button>
            `).join('')}
          </div>
        </div>

        <div style="font-size:1.5rem; font-weight:800; color:#1E3B2A; margin-bottom:20px;" id="qv-price-display">
          ${formatCurrency(defaultVariant.price)}
        </div>

        <div style="display:flex; gap:12px;">
          <button class="btn btn-outline" onclick="window.cart.addItem('${product.id}', window.qvSelectedVariant || '${defaultVariant.label}', 1); document.getElementById('quickview-modal').classList.remove('active');">
            Thêm Vào Giỏ
          </button>
          <button class="btn btn-primary" onclick="window.cart.addItem('${product.id}', window.qvSelectedVariant || '${defaultVariant.label}', 1); document.getElementById('quickview-modal').classList.remove('active'); window.cart.openDrawer();">
            Mua Ngay
          </button>
        </div>
      </div>
    </div>
  `;

  window.qvSelectedVariant = defaultVariant.label;
  modal.classList.add('active');
};

window.selectQuickViewVariant = function(productId, variantLabel, price, btn) {
  window.qvSelectedVariant = variantLabel;
  const parent = btn.parentElement;
  parent.querySelectorAll('.variant-btn').forEach(b => b.classList.remove('active'));
  btn.classList.add('active');
  const priceDisplay = document.getElementById('qv-price-display');
  if (priceDisplay) priceDisplay.textContent = formatCurrency(price);
};

/* Prefill quote form when clicking B2B link */
window.prefillQuoteForProduct = function(productName) {
  const noteField = document.getElementById('notes');
  if (noteField) {
    noteField.value = `Quan tâm báo giá số lượng lớn cho sản phẩm: ${productName}`;
  }
  const wholesaleRadio = document.querySelector('input[name="demand"][value="wholesale"]');
  if (wholesaleRadio) {
    wholesaleRadio.checked = true;
  }
};

/* Mobile Drawer */
function initMobileDrawer() {
  const menuBtn = document.getElementById('mobile-menu-btn');
  const drawer = document.getElementById('mobile-nav-drawer');
  const overlay = document.getElementById('drawer-overlay');
  const closeBtn = document.getElementById('mobile-nav-close');
  const links = document.querySelectorAll('.mobile-nav-link');

  if (menuBtn && drawer) {
    menuBtn.addEventListener('click', () => {
      drawer.classList.add('active');
      if (overlay) overlay.classList.add('active');
      document.body.style.overflow = 'hidden';
    });
  }

  function closeMobileDrawer() {
    if (drawer) drawer.classList.remove('active');
    if (overlay) overlay.classList.remove('active');
    document.body.style.overflow = '';
  }

  if (closeBtn) closeBtn.addEventListener('click', closeMobileDrawer);
  links.forEach(l => l.addEventListener('click', closeMobileDrawer));
}

/* Floating widgets & scroll to top */
function initFloatingWidgets() {
  const scrollTopBtn = document.getElementById('scroll-top-btn');
  if (scrollTopBtn) {
    window.addEventListener('scroll', () => {
      if (window.scrollY > 400) {
        scrollTopBtn.classList.add('visible');
      } else {
        scrollTopBtn.classList.remove('visible');
      }
    });

    scrollTopBtn.addEventListener('click', () => {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
  }
}
