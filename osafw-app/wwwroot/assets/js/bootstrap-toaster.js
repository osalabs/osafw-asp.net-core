/**
 * Toaster - Bootstrap 5 Toast Manager Library
 * https://github.com/osalabs/bootstrap-toaster
 * Copyright 2024 Oleg Savchuk
 * Licensed under MIT (https://github.com/osalabs/bootstrap-toaster/blob/main/LICENSE)
 *
 *
 * A lightweight and customizable toast notification library built on top of Bootstrap 5.
 *
 * Features:
 * - Supports HTML content.
 * - Customizable themes.
 * - Provides helper methods for common toast types (success, danger, info, primary).
 * - Includes a "Close All" feature with debounced update for performance.
 * - Emits custom events for toast actions.
 *
 * Usage Examples:
 *
 * // Showing a simple toast
 * Toast('Hello World');
 *
 * // Showing a success toast, stay until user closed
 * ToastSuccess('Operation completed successfully', { autohide: false });
 *
 * // Custom toast with options
 * Toast('Custom Message', { theme: 'text-bg-info', html: true, delay: 7000 });
 *
 * Custom Events:
 * Toast events can be listened to on the document body for actions like 'toast.shown', 'toast.hidden', and 'toast.hidden.all'.
 *
 * document.body.addEventListener('toast.shown', function(event) {
 *     console.log('Toast shown:', event.detail.message);
 * });
 */

(function(window){
    let toastContainer;
    let debounceTimer;

    function createToastContainer() {
        if (!toastContainer) {
            toastContainer = document.createElement('div');
            toastContainer.style.position = 'fixed';
            toastContainer.style.top = '20px';
            toastContainer.style.right = '20px';
            toastContainer.style.zIndex = '1050';
            document.body.appendChild(toastContainer);
        }
    }

    function debounceCloseAllUpdate() {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(updateCloseAllToast, 100);
    }

    function updateCloseAllToast() {
        let closeAllToast = document.getElementById('close-all-toast');
        let activeToasts = toastContainer.children.length;
        if (activeToasts > 1 && !closeAllToast) {
            closeAllToast = document.createElement('div');
            closeAllToast.id = 'close-all-toast';
            closeAllToast.className = 'toast align-items-center text-bg-secondary';
            closeAllToast.style.position = 'relative';
            closeAllToast.style.minWidth = '250px';
            closeAllToast.style.marginBottom = '10px';
            closeAllToast.style.cursor = 'pointer';
            closeAllToast.innerHTML = `<div class="text-center">close all</div>`;
            closeAllToast.setAttribute('data-bs-autohide', 'false');
            closeAllToast.onclick = function() {
                Array.from(toastContainer.children).forEach(toastEl => {
                    const toastInstance = bootstrap.Toast.getInstance(toastEl);
                    if (toastInstance) {
                        toastInstance.hide();
                    }
                });
                document.body.dispatchEvent(new CustomEvent('toast.hidden.all'));
            };
            toastContainer.prepend(closeAllToast); // Add the Close All toast at the top
            bootstrap.Toast.getOrCreateInstance(closeAllToast).show();
        } else if (activeToasts <= 1 && closeAllToast) {
            closeAllToast.remove();
        }
    }

    function createToast(message, options = {}) {
        createToastContainer();
        options = {
            theme: 'text-bg-secondary', // Default theme
            animation: options.animation !== undefined ? options.animation : true,
            autohide: options.autohide !== false, // defaults to true unless explicitly set to false
            delay: options.delay || 5000,
            html: options.html || false,
            ...options
        };

        const toastEl = document.createElement('div');
        toastEl.className = `toast align-items-center ${options.theme} ${options.animation ? '' : 'fade'}`;
        toastEl.role = 'alert';
        toastEl.ariaLive = 'assertive';
        toastEl.ariaAtomic = 'true';
        toastEl.style.position = 'relative';
        toastEl.style.minWidth = '250px';
        toastEl.style.marginBottom = '10px';

        // Header and Body content handling
        if (options.header) {
            const toastHeader = document.createElement('div');
            toastHeader.className = 'toast-header';
            if (options.html) {
                toastHeader.innerHTML = options.header;
            } else {
                toastHeader.appendChild(document.createTextNode(options.header));
            }
            toastEl.appendChild(toastHeader);
        }

        const toastBody = document.createElement('div');
        toastBody.className = 'toast-body';
        if (options.html) {
            toastBody.innerHTML = message;
        } else {
            toastBody.appendChild(document.createTextNode(message));
        }
        toastEl.appendChild(toastBody);

        // Close button (if in body - make it white for better contrast)
        const button = `<button type="button" class="btn-close ${options.header ? '' : 'btn-close-white'}" data-bs-dismiss="toast" aria-label="Close" style="position: absolute; right: 10px; top: 10px;"></button>`;
        toastEl.insertAdjacentHTML('beforeend', button);

        toastContainer.appendChild(toastEl);

        const bsToast = new bootstrap.Toast(toastEl, {
            animation: options.animation,
            autohide: options.autohide,
            delay: options.delay
        });
        bsToast.show();

        toastEl.addEventListener('shown.bs.toast', function () {
            document.body.dispatchEvent(new CustomEvent('toast.shown', { detail: { message: message } }));
        });
        toastEl.addEventListener('hidden.bs.toast', function () {
            this.remove();
            document.body.dispatchEvent(new CustomEvent('toast.hidden', { detail: { message: message } }));
            debounceCloseAllUpdate();
        });

        debounceCloseAllUpdate();
    }

    // Helper functions for common use cases
    window.Toast = createToast;
    window.ToastSuccess = function(message, options) { createToast(message, { ...options, theme: 'text-bg-success' }); };
    window.ToastWarning = function(message, options) { createToast(message, { ...options, theme: 'text-bg-warning' }); };
    window.ToastDanger = function(message, options) { createToast(message, { ...options, theme: 'text-bg-danger' }); };
    window.ToastInfo = function(message, options) { createToast(message, { ...options, theme: 'text-bg-info' }); };
    window.ToastPrimary = function(message, options) { createToast(message, { ...options, theme: 'text-bg-primary' }); };
})(window);
