/**
 * Wonder Watch - Secure Checkout & Razorpay Integration
 * Architecture: Vanilla ES6 + Fetch API + Razorpay SDK
 */

document.addEventListener('DOMContentLoaded', () => {
    const checkoutForm = document.getElementById('checkout-form');
    const payButton = document.getElementById('btn-pay-now');
    const btnSpinner = document.getElementById('btn-spinner');
    const errorContainer = document.getElementById('checkout-error');

    const newAddressForm = document.getElementById('NewAddressForm');
    const addressRadios = document.querySelectorAll('.address-radio');

    // Toggle manual address form visibility
    addressRadios.forEach(radio => {
        radio.addEventListener('change', (e) => {
            if (e.target.value === 'new') {
                newAddressForm.classList.remove('max-h-0', 'opacity-0', 'pointer-events-none');
                newAddressForm.classList.add('max-h-[800px]', 'opacity-100');
                newAddressForm.removeAttribute('inert');
            } else {
                newAddressForm.classList.remove('max-h-[800px]', 'opacity-100');
                newAddressForm.classList.add('max-h-0', 'opacity-0', 'pointer-events-none');
                newAddressForm.setAttribute('inert', '');
            }
        });
    });

    // Handle remove product buttons
    const removeButtons = document.querySelectorAll('.remove-checkout-item-btn');
    removeButtons.forEach(btn => {
        btn.addEventListener('click', async (e) => {
            const watchId = e.currentTarget.dataset.watchId;
            const csrfTokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
            if (!csrfTokenInput) return;

            try {
                e.currentTarget.style.opacity = '0.5';
                e.currentTarget.style.pointerEvents = 'none';

                const response = await fetch('/api/cart/update', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': csrfTokenInput.value
                    },
                    body: JSON.stringify({
                        WatchId: watchId,
                        Quantity: 0
                    })
                });

                if (response.ok) {
                    window.location.reload();
                } else {
                    console.error('Failed to update cart');
                    e.currentTarget.style.opacity = '1';
                    e.currentTarget.style.pointerEvents = 'auto';
                }
            } catch (err) {
                console.error('Network error updating cart', err);
                e.currentTarget.style.opacity = '1';
                e.currentTarget.style.pointerEvents = 'auto';
            }
        });
    });

    if (!checkoutForm) return;

    checkoutForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        // 1. Clear previous errors and set loading state
        hideError();
        setLoadingState(true);

        // 2. Extract CSRF Token
        const csrfTokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        if (!csrfTokenInput) {
            showError('Security token missing. Please refresh the page.');
            setLoadingState(false);
            return;
        }
        const csrfToken = csrfTokenInput.value;

        // 3. Gather Form Data dynamically based on selection
        const selectedRadio = document.querySelector('input[name="AddressSelection"]:checked');
        let formData = {};

        if (selectedRadio && selectedRadio.value === 'saved') {
            formData = {
                Line1: selectedRadio.dataset.line1,
                Line2: selectedRadio.dataset.line2 || '',
                City: selectedRadio.dataset.city,
                State: selectedRadio.dataset.state,
                PinCode: selectedRadio.dataset.pincode,
                Phone: document.getElementById('Phone').value.trim() || selectedRadio.dataset.phone 
            };
        } else {
            // New Address Form validation
            const line1 = document.getElementById('Line1').value.trim();
            const city = document.getElementById('City').value.trim();
            const state = document.getElementById('State').value.trim();
            const pinCode = document.getElementById('PinCode').value.trim();
            
            if (!line1 || !city || !state || !pinCode) {
                showError('Please fill in all required shipping address fields.');
                setLoadingState(false);
                return;
            }

            formData = {
                Line1: line1,
                Line2: document.getElementById('Line2').value.trim(),
                City: city,
                State: state,
                PinCode: pinCode,
                Phone: document.getElementById('Phone').value.trim()
            };
        }

        try {
            // 4. Step 1: Create Order on Server
            const createResponse = await fetch('/checkout/create-order', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': csrfToken
                },
                body: JSON.stringify(formData)
            });

            const createResult = await createResponse.json();

            if (!createResponse.ok || !createResult.success) {
                throw new Error(createResult.error || 'Failed to initialize secure checkout.');
            }

            // 5. Step 2: Initialize Razorpay Modal
            const options = {
                key: createResult.keyId,
                amount: createResult.amount, // Amount is in paise
                currency: "INR",
                name: "Wonder Watch",
                description: "Acquisition of Horological Masterpieces",
                image: "/images/brand/og-image.webp", // Assuming a brand logo exists here
                order_id: createResult.razorpayOrderId,
                handler: async function (response) {
                    // 6. Step 3: Verify Payment on Server
                    await verifyPayment(
                        response.razorpay_order_id,
                        response.razorpay_payment_id,
                        response.razorpay_signature,
                        csrfToken
                    );
                },
                prefill: {
                    name: createResult.prefill.name,
                    email: createResult.prefill.email,
                    contact: createResult.prefill.contact
                },
                theme: {
                    color: "#C9A74A" // Wonder Watch Gold
                },
                modal: {
                    ondismiss: function () {
                        // User closed the modal without completing payment
                        setLoadingState(false);
                        showError('Payment was cancelled. You can try again when ready.');
                    }
                }
            };

            const rzp = new Razorpay(options);

            rzp.on('payment.failed', function (response) {
                setLoadingState(false);
                showError(`Payment failed: ${response.error.description}`);
            });

            rzp.open();

        } catch (error) {
            console.error('Checkout Error:', error);
            showError(error.message);
            setLoadingState(false);
        }
    });

    /**
     * Verifies the Razorpay signature with the server
     */
    async function verifyPayment(razorpayOrderId, paymentId, signature, csrfToken) {
        try {
            const verifyResponse = await fetch('/checkout/verify', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': csrfToken
                },
                body: JSON.stringify({
                    RazorpayOrderId: razorpayOrderId,
                    RazorpayPaymentId: paymentId,
                    RazorpaySignature: signature
                })
            });

            const verifyResult = await verifyResponse.json();

            if (!verifyResponse.ok || !verifyResult.success) {
                throw new Error(verifyResult.error || 'Payment verification failed. Please contact concierge.');
            }

            // 7. Step 4: Redirect to Confirmation Page
            window.location.href = verifyResult.redirectUrl;

        } catch (error) {
            console.error('Verification Error:', error);
            showError(error.message);
            setLoadingState(false);
        }
    }

    /**
     * Toggles the loading spinner and button disabled state
     */
    function setLoadingState(isLoading) {
        if (isLoading) {
            payButton.disabled = true;
            payButton.classList.add('opacity-80', 'cursor-not-allowed');
            btnSpinner.classList.remove('hidden');
        } else {
            payButton.disabled = false;
            payButton.classList.remove('opacity-80', 'cursor-not-allowed');
            btnSpinner.classList.add('hidden');
        }
    }

    /**
     * Displays an error message in the designated container
     */
    function showError(message) {
        errorContainer.textContent = message;
        errorContainer.classList.remove('hidden');
        // Scroll to error
        errorContainer.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }

    /**
     * Hides the error container
     */
    function hideError() {
        errorContainer.classList.add('hidden');
        errorContainer.textContent = '';
    }
});