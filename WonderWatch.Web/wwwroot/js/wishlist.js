/**
 * Wonder Watch - Wishlist State & UI Controller
 * Architecture: Vanilla ES6 + Fetch API
 */

document.addEventListener('DOMContentLoaded', () => {

    // Select all wishlist toggle buttons across the application
    const wishlistButtons = document.querySelectorAll('.js-toggle-wishlist');

    wishlistButtons.forEach(btn => {
        btn.addEventListener('click', async (e) => {
            e.preventDefault();

            const watchId = btn.getAttribute('data-watch-id');
            if (!watchId) return;

            // 1. Extract CSRF Token from anywhere in the DOM
            const csrfInput = document.querySelector('input[name="__RequestVerificationToken"]');
            const csrfToken = csrfInput ? csrfInput.value : '';

            // 2. Optimistic UI Update (Optional, but we'll wait for server confirmation for accuracy)
            const originalHtml = btn.innerHTML;

            try {
                // 3. Execute API Call
                const response = await fetch('/api/wishlist/toggle', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': csrfToken,
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: JSON.stringify({ watchId })
                });

                // 4. Handle Unauthenticated Users
                if (response.status === 401 || response.redirected || response.type === 'opaqueredirect') {
                    alert('Please log in to add items to your wishlist.');
                    const currentUrl = encodeURIComponent(window.location.pathname + window.location.search);
                    window.location.href = `/account/login?returnUrl=${currentUrl}`;
                    return;
                }

                // 5. Handle Success
                if (response.ok) {
                    const data = await response.json();

                    if (data.success) {
                        const icon = btn.querySelector('svg');
                        const text = btn.querySelector('.wishlist-text');

                        if (data.isWishlisted) {
                            // State: Added to Wishlist
                            if (icon) icon.setAttribute('fill', 'currentColor');
                            if (text) text.textContent = 'Wishlisted';
                            btn.classList.add('text-gold');
                            btn.classList.remove('text-muted');
                        } else {
                            // State: Removed from Wishlist
                            if (icon) icon.setAttribute('fill', 'none');
                            if (text) text.textContent = 'Wishlist';
                            btn.classList.remove('text-gold');

                            // Special Case: If we are on the Vault Wishlist page, remove the card entirely
                            if (window.location.pathname.toLowerCase().includes('/vault/wishlist')) {
                                const card = btn.closest('.group');
                                if (card) {
                                    card.style.transition = 'opacity 400ms ease, transform 400ms ease';
                                    card.style.opacity = '0';
                                    card.style.transform = 'scale(0.95)';
                                    setTimeout(() => card.remove(), 400);
                                }
                            }
                        }
                    }
                } else {
                    console.error('Failed to toggle wishlist state. Server returned:', response.status);
                }
            } catch (error) {
                console.error('Error toggling wishlist:', error);
                btn.innerHTML = originalHtml; // Revert on critical failure
            }
        });
    });
});