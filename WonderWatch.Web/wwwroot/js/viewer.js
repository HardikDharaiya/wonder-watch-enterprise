/**
 * Wonder Watch - 3D Product Viewer
 * Architecture: Three.js r128 + GLTFLoader + DRACOLoader
 */

class WatchViewer {
    constructor(canvas) {
        // [ROLE-3] SACRED LAW: Mobile check is ALWAYS line 1 of init.
        if (canvas.dataset.mobile === 'true') {
            console.log('Mobile device detected. 3D WebGL initialization aborted. Showing static fallback.');
            return;
        }

        this.canvas = canvas;
        this.modelPath = canvas.dataset.modelPath;

        // Read dynamic configuration from data attributes
        this.cameraZ = parseFloat(canvas.dataset.cameraZ) || 15;
        this.isInteractive = canvas.dataset.interactive !== 'false'; // Defaults to true
        this.autoRotateSpeed = canvas.dataset.autoRotate === 'true' ? 0.005 : 0.001;

        // FIXED: Added dynamic scale and ambient light properties
        this.modelScale = parseFloat(canvas.dataset.modelScale) || 1;
        this.ambientLightIntensity = parseFloat(canvas.dataset.ambientLight) || 0.3;

        this.isVisible = false;
        this.animationId = null;

        if (!this.modelPath) {
            console.warn('No GLB model path provided.');
            return;
        }

        this.init();
    }

    init() {
        // 1. Scene Setup
        this.scene = new THREE.Scene();

        // 2. Camera Setup
        this.camera = new THREE.PerspectiveCamera(45, this.canvas.clientWidth / this.canvas.clientHeight, 0.1, 100);
        this.initialCameraPosition = new THREE.Vector3(0, 0, this.cameraZ);
        this.camera.position.copy(this.initialCameraPosition);

        // 3. Renderer Setup (Transparent background to blend with bg-surface-alt/void)
        this.renderer = new THREE.WebGLRenderer({
            canvas: this.canvas,
            antialias: true,
            alpha: true
        });
        this.renderer.setSize(this.canvas.clientWidth, this.canvas.clientHeight);
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2)); // Cap pixel ratio for performance
        this.renderer.outputEncoding = THREE.sRGBEncoding;
        this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
        this.renderer.toneMappingExposure = 1.2;

        // 4. Lighting & Environment (Dark Luxury Studio Setup)
        this.setupLighting();

        // 5. Controls Setup (Conditional based on data-interactive)
        if (this.isInteractive) {
            this.controls = new THREE.OrbitControls(this.camera, this.renderer.domElement);
            this.controls.enableDamping = true;
            this.controls.dampingFactor = 0.05; // Heavy, smooth feel
            this.controls.enablePan = false;
            this.controls.minDistance = 5;
            this.controls.maxDistance = 25;
            this.controls.target.set(0, 0, 0);
        }

        // 6. Load Model
        this.loadModel();

        // 7. Event Listeners
        window.addEventListener('resize', this.onWindowResize.bind(this));

        if (this.isInteractive) {
            const resetBtn = document.getElementById('reset-camera-btn');
            if (resetBtn) {
                resetBtn.addEventListener('click', this.resetCamera.bind(this));
            }
        }

        // 8. Intersection Observer for Performance (Pause when off-screen)
        this.setupObserver();
    }

    setupLighting() {
        // FIXED: Apply dynamic ambient light intensity so dark models become visible
        const ambientLight = new THREE.AmbientLight(0xffffff, this.ambientLightIntensity);
        this.scene.add(ambientLight);

        // Key Light (Warm Gold tint)
        const keyLight = new THREE.DirectionalLight(0xfff5e6, 2.5);
        keyLight.position.set(5, 5, 5);
        this.scene.add(keyLight);

        // Fill Light (Cool blue tint for contrast)
        const fillLight = new THREE.DirectionalLight(0xe6f0ff, 1.0);
        fillLight.position.set(-5, 3, 5);
        this.scene.add(fillLight);

        // Rim Light (Sharp white to highlight metal edges)
        const rimLight = new THREE.SpotLight(0xffffff, 5);
        rimLight.position.set(0, 5, -5);
        rimLight.lookAt(0, 0, 0);
        rimLight.penumbra = 0.5;
        this.scene.add(rimLight);

        // Generate Environment Map for PBR Reflections
        const pmremGenerator = new THREE.PMREMGenerator(this.renderer);
        pmremGenerator.compileEquirectangularShader();
        this.scene.environment = pmremGenerator.fromScene(this.scene).texture;
    }

    loadModel() {
        const dracoLoader = new THREE.DRACOLoader();
        // Point to Google's official static decoder CDN
        dracoLoader.setDecoderPath('https://www.gstatic.com/draco/versioned/decoders/1.4.1/');

        const loader = new THREE.GLTFLoader();
        loader.setDRACOLoader(dracoLoader);

        loader.load(
            this.modelPath,
            (gltf) => {
                this.model = gltf.scene;

                // Center the model
                const box = new THREE.Box3().setFromObject(this.model);
                const center = box.getCenter(new THREE.Vector3());
                this.model.position.sub(center);

                // Ensure camera bounds exist so the model doesn't load out-of-scale
                const size = box.getSize(new THREE.Vector3());
                const maxDim = Math.max(size.x, size.y, size.z);
                const targetSize = 10; // Fit within 10 units
                let dynamicScale = (targetSize / maxDim);
                
                // If modelScale is explicitly passed as != 1 and valid, apply it, else use normalized
                const finalScale = this.modelScale !== 1 ? this.modelScale : dynamicScale;
                this.model.scale.set(finalScale, finalScale, finalScale);

                // Apply subtle initial rotation
                this.model.rotation.y = -Math.PI / 8;

                this.scene.add(this.model);
            },
            undefined,
            (error) => {
                console.error('An error happened loading the GLB model:', error);
            }
        );
    }

    setupObserver() {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                this.isVisible = entry.isIntersecting;
                if (this.isVisible) {
                    this.animate();
                } else {
                    this.pause();
                }
            });
        }, { threshold: 0.1 });

        observer.observe(this.canvas);
    }

    animate() {
        if (!this.isVisible) return;

        this.animationId = requestAnimationFrame(this.animate.bind(this));

        // Auto-rotate the model based on the configured speed
        if (this.model) {
            // Only rotate if controls don't exist, or if they exist but the user isn't actively dragging
            if (!this.controls || (this.controls && !this.controls.state)) {
                this.model.rotation.y += this.autoRotateSpeed;
            }
        }

        if (this.controls) {
            this.controls.update(); // Required for damping
        }

        this.renderer.render(this.scene, this.camera);
    }

    pause() {
        if (this.animationId) {
            cancelAnimationFrame(this.animationId);
            this.animationId = null;
        }
    }

    resetCamera() {
        if (!this.controls) return;

        // Smoothly animate back to initial position
        this.camera.position.copy(this.initialCameraPosition);
        this.controls.target.set(0, 0, 0);
        if (this.model) {
            this.model.rotation.set(0, -Math.PI / 8, 0);
        }
        this.controls.update();
    }

    onWindowResize() {
        const parent = this.canvas.parentElement;
        this.camera.aspect = parent.clientWidth / parent.clientHeight;
        this.camera.updateProjectionMatrix();
        this.renderer.setSize(parent.clientWidth, parent.clientHeight);
    }
}

// Initialize on DOM Content Loaded
document.addEventListener('DOMContentLoaded', () => {
    const canvas = document.getElementById('three-canvas');
    if (canvas) {
        new WatchViewer(canvas);
    }
});