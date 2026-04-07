/**
 * Wonder Watch - Secure Checkout & Razorpay Integration
 * Architecture: Vanilla ES6 + Fetch API + Razorpay SDK
 */

document.addEventListener('DOMContentLoaded', () => {
    const checkoutForm = document.getElementById('checkout-form');
    const payButton = document.getElementById('btn-pay-now');
    const btnSpinner = document.getElementById('btn-spinner');
    const errorContainer = document.getElementById('checkout-error');

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

        // 3. Gather Form Data
        const formData = {
            Line1: document.getElementById('Line1').value.trim(),
            Line2: document.getElementById('Line2').value.trim(),
            City: document.getElementById('City').value.trim(),
            State: document.getElementById('State').value.trim(),
            PinCode: document.getElementById('PinCode').value.trim(),
            Phone: document.getElementById('Phone').value.trim()
        };

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
                        createResult.orderId,
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
    async function verifyPayment(orderId, razorpayOrderId, paymentId, signature, csrfToken) {
        try {
            const verifyResponse = await fetch('/checkout/verify', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': csrfToken
                },
                body: JSON.stringify({
                    OrderId: orderId,
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