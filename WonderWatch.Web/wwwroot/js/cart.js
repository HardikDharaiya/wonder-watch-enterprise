/**
 * Wonder Watch - Cart State & UI Controller
 * Architecture: Vanilla ES6 + Fetch API
 */

document.addEventListener('DOMContentLoaded', () => {

    // DOM Elements
    const cartToggleBtn = document.getElementById('nav-cart-toggle');
    const cartDrawer = document.getElementById('cart-drawer');
    const cartBackdrop = document.getElementById('cart-backdrop');
    const cartCloseBtn = document.getElementById('cart-close-btn');
    const cartBadge = document.getElementById('nav-cart-badge');
    const cartItemsContainer = document.getElementById('cart-items-container');
    const cartEmptyState = document.getElementById('cart-empty-state');
    const cartFooter = document.getElementById('cart-footer');
    const cartSubtotal = document.getElementById('cart-subtotal');
    const cartItemTemplate = document.getElementById('cart-item-template');
    const continueShoppingBtn = document.getElementById('cart-continue-shopping-btn');

    // Add to Cart Buttons (NodeList)
    const addToCartButtons = document.querySelectorAll('.js-add-to-cart');

    // --- DRAWER UI LOGIC ---

    const openCart = () => {
        cartBackdrop.classList.remove('opacity-0', 'pointer-events-none');
        cartBackdrop.classList.add('opacity-100');
        cartDrawer.classList.remove('translate-x-full');
        cartDrawer.classList.add('translate-x-0');
        document.body.style.overflow = 'hidden'; // Prevent background scrolling
        fetchCartSummary(); // Refresh data when opened
    };

    const closeCart = () => {
        cartBackdrop.classList.remove('opacity-100');
        cartBackdrop.classList.add('opacity-0', 'pointer-events-none');
        cartDrawer.classList.remove('translate-x-0');
        cartDrawer.classList.add('translate-x-full');
        document.body.style.overflow = '';
    };

    if (cartToggleBtn) cartToggleBtn.addEventListener('click', openCart);
    if (cartCloseBtn) cartCloseBtn.addEventListener('click', closeCart);
    if (cartBackdrop) cartBackdrop.addEventListener('click', closeCart);
    if (continueShoppingBtn) {
        continueShoppingBtn.addEventListener('click', () => {
            closeCart();
            window.location.href = '/catalog';
        });
    }

    // --- AJAX API LOGIC ---

    const updateCartBadge = (count) => {
        if (!cartBadge) return;
        if (count > 0) {
            cartBadge.classList.remove('hidden');
        } else {
            cartBadge.classList.add('hidden');
        }
    };

    const renderCartItems = (items, subtotalFormatted) => {
        cartItemsContainer.innerHTML = ''; // Clear current items

        if (items.length === 0) {
            cartEmptyState.classList.remove('hidden');
            cartFooter.classList.add('hidden');
        } else {
            cartEmptyState.classList.add('hidden');
            cartFooter.classList.remove('hidden');
            cartSubtotal.textContent = subtotalFormatted;

            items.forEach(item => {
                // Clone the HTML template
                const clone = cartItemTemplate.content.cloneNode(true);

                // Populate data
                clone.querySelector('.item-brand').textContent = item.brand;

                const nameLinks = clone.querySelectorAll('.item-link');
                nameLinks.forEach(link => {
                    link.href = `/catalog/${item.slug}`;
                    if (link.classList.contains('font-medium')) {
                        link.textContent = item.name;
                    }
                });

                clone.querySelector('.item-image').src = item.imageUrl;
                clone.querySelector('.item-image').alt = item.name;
                clone.querySelector('.item-quantity').textContent = item.quantity;
                clone.querySelector('.item-total').textContent = item.itemTotalFormatted;

                // Wire up quantity buttons
                const decreaseBtn = clone.querySelector('.item-decrease-btn');
                const increaseBtn = clone.querySelector('.item-increase-btn');

                decreaseBtn.addEventListener('click', () => updateQuantity(item.watchId, item.quantity - 1));
                increaseBtn.addEventListener('click', () => updateQuantity(item.watchId, item.quantity + 1));

                // Wire up remove button
                const removeBtn = clone.querySelector('.item-remove-btn');
                removeBtn.addEventListener('click', () => removeFromCart(item.watchId));

                cartItemsContainer.appendChild(clone);
            });
        }
    };

    const fetchCartSummary = async () => {
        try {
            const response = await fetch('/api/cart/summary');
            if (response.ok) {
                const data = await response.json();
                updateCartBadge(data.count);
                renderCartItems(data.items, data.subtotalFormatted);
            }
        } catch (error) {
            console.error('Failed to fetch cart summary:', error);
        }
    };

    const addToCart = async (watchId, quantity = 1) => {
        try {
            const response = await fetch('/api/cart/add', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ watchId, quantity })
            });

            if (response.ok) {
                const data = await response.json();
                updateCartBadge(data.count);
                openCart(); // Slide drawer open to show success
            } else {
                alert('Failed to add item to cart. It may be out of stock.');
            }
        } catch (error) {
            console.error('Error adding to cart:', error);
        }
    };

    const removeFromCart = async (watchId) => {
        try {
            const response = await fetch('/api/cart/remove', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ watchId })
            });

            if (response.ok) {
                fetchCartSummary(); // Refresh the drawer UI
            }
        } catch (error) {
            console.error('Error removing from cart:', error);
        }
    };

    const updateQuantity = async (watchId, quantity) => {
        if (quantity <= 0) {
            return removeFromCart(watchId);
        }
        try {
            const response = await fetch('/api/cart/update', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ watchId, quantity })
            });

            if (response.ok) {
                fetchCartSummary(); // Refresh the drawer UI
            }
        } catch (error) {
            console.error('Error updating quantity:', error);
        }
    };

    // --- BIND ADD TO CART BUTTONS ---
    addToCartButtons.forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            const watchId = btn.getAttribute('data-watch-id');
            if (watchId) {
                // Change button text temporarily for UX
                const originalText = btn.textContent;
                btn.textContent = 'Adding...';
                btn.disabled = true;

                addToCart(watchId).finally(() => {
                    btn.textContent = originalText;
                    btn.disabled = false;
                });
            }
        });
    });

    // Initialize badge on page load
    fetchCartSummary();
});