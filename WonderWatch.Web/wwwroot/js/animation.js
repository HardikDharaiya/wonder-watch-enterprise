/**
 * Wonder Watch - Global Animation & Interaction Controller
 * Architecture: Vanilla ES6 + IntersectionObserver + requestAnimationFrame
 * Motion Curve: cubic-bezier(0.16, 1, 0.3, 1) 600ms
 */

document.addEventListener('DOMContentLoaded', () => {

    // 1. PAGE LOAD FADE-IN
    document.body.style.opacity = '0';
    document.body.style.transition = 'opacity 600ms cubic-bezier(0.16, 1, 0.3, 1)';

    requestAnimationFrame(() => {
        document.body.style.opacity = '1';
    });

    // 2. NAVBAR SCROLL EFFECT (Updated for 96px height and transparency)
    const header = document.querySelector('header');
    if (header) {
        const handleScroll = () => {
            if (window.scrollY > 50) {
                header.classList.add('shadow-[0_10px_30px_rgba(0,0,0,0.5)]');
                header.style.backgroundColor = 'rgba(10, 10, 10, 0.98)';
                header.style.borderBottomColor = 'rgba(201, 167, 74, 0.2)';
            } else {
                header.classList.remove('shadow-[0_10px_30px_rgba(0,0,0,0.5)]');
                header.style.backgroundColor = 'transparent';
                header.style.borderBottomColor = 'transparent';
            }
        };

        window.addEventListener('scroll', handleScroll, { passive: true });
        handleScroll();
    }

    // 3. REVEAL ON SCROLL
    const revealElements = document.querySelectorAll('.group, section h2, section p, .bg-surface, .bg-surface-alt');
    if (revealElements.length > 0) {
        revealElements.forEach(el => {
            if (el.closest('header') || el.closest('#cart-drawer') || el.closest('aside')) return;
            el.style.opacity = '0';
            el.style.transform = 'translateY(20px)';
            el.style.transition = 'opacity 800ms cubic-bezier(0.16, 1, 0.3, 1), transform 800ms cubic-bezier(0.16, 1, 0.3, 1)';
        });

        const revealObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const target = entry.target;
                    target.style.opacity = '1';
                    target.style.transform = 'translateY(0)';
                    observer.unobserve(target);
                }
            });
        }, { threshold: 0.1, rootMargin: "0px 0px -50px 0px" });

        revealElements.forEach(el => revealObserver.observe(el));
    }

    // 4. FIXED: INFINITE MARQUEE (Track-Based Logic)
    const marquees = document.querySelectorAll('.js-marquee');
    marquees.forEach(marquee => {
        // Prevent double initialization if script runs twice
        if (marquee.dataset.initialized === 'true') return;

        const originalContent = marquee.innerHTML;

        // Clear and rebuild with a single moving track
        marquee.innerHTML = `
            <div class="marquee-track flex items-center whitespace-nowrap will-change-transform">
                <div class="marquee-content flex items-center">${originalContent}</div>
                <div class="marquee-content flex items-center">${originalContent}</div>
            </div>
        `;

        const track = marquee.querySelector('.marquee-track');
        const contentNodes = marquee.querySelectorAll('.marquee-content');

        // Ensure children don't shrink to fit container
        contentNodes.forEach(node => {
            Array.from(node.children).forEach(child => {
                child.classList.add('flex-shrink-0');
            });
        });

        let progress = 0;
        const speed = 0.8; // Pixels per frame

        const animate = () => {
            progress -= speed;

            // Reset point is the width of exactly one content block
            const resetPoint = contentNodes[0].offsetWidth;

            if (Math.abs(progress) >= resetPoint) {
                progress = 0;
            }

            track.style.transform = `translateX(${progress}px)`;
            requestAnimationFrame(animate);
        };

        marquee.dataset.initialized = 'true';
        animate();
    });

    // 5. SMOOTH SCROLLING (Updated for 96px header offset)
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            const targetId = this.getAttribute('href');
            if (targetId === '#') return;
            const targetElement = document.querySelector(targetId);
            if (targetElement) {
                e.preventDefault();
                const headerOffset = 96;
                const elementPosition = targetElement.getBoundingClientRect().top;
                const offsetPosition = elementPosition + window.pageYOffset - headerOffset;
                window.scrollTo({ top: offsetPosition, behavior: 'smooth' });
            }
        });
    });
});