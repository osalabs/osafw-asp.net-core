(function() {
    const modalContainer = document.createElement('div');
    document.body.appendChild(modalContainer);

    function createModal(title, icon, content, buttons, modalClass = '', isHtml = false, focusIndex = 0, escValue = null, classDialog = '', classContent = '') {
        return new Promise((resolve) => {
            // Modal HTML structure
            const modalId = `modal-${Math.random().toString(36).substr(2, 9)}`;
            const modalHtml = `
                <div class="modal fade ${modalClass}" id="${modalId}" tabindex="-1" aria-labelledby="${modalId}-label" aria-hidden="true">
                    <div class="modal-dialog ${classDialog}">
                        <div class="modal-content ${classContent}">
                            <div class="modal-header">
                                <h5 class="modal-title" id="${modalId}-label">${title}</h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                            </div>
                            <div class="modal-body d-flex align-items-center">
                                <i class="bi ${icon} me-3 fs-1"></i>
                                ${isHtml ? content : `<p>${content}</p>`}
                            </div>
                            <div class="modal-footer">
                                ${buttons.map((button, index) => `
                                    <button type="button" class="btn ${button.class}" data-value="${button.value}" data-index="${index}">${button.text}</button>
                                `).join('')}
                            </div>
                        </div>
                    </div>
                </div>
            `;

            modalContainer.insertAdjacentHTML('beforeend', modalHtml);
            const modalElement = document.getElementById(modalId);

            // Show the modal
            const bootstrapModal = new bootstrap.Modal(modalElement);
            bootstrapModal.show();

            // Handle button clicks
            modalElement.querySelectorAll('.modal-footer .btn').forEach((button) => {
                button.addEventListener('click', (event) => {
                    const value = event.target.getAttribute('data-value');
                    resolve(value);
                    bootstrapModal.hide();
                });
            });

            // Handle Esc key and close button
            modalElement.addEventListener('hidden.bs.modal', () => {
                resolve(escValue);
                modalElement.remove();
            });

            // Set focus on the specified button
            bootstrapModal._element.addEventListener('shown.bs.modal', () => {
                const focusButton = modalElement.querySelector(`.modal-footer .btn[data-index="${focusIndex}"]`);
                if (focusButton) {
                    focusButton.focus();
                }
            });
        });
    }

    window.alert = async function(text, options = {}) {
        const { title = 'Alert', icon = 'bi-exclamation-triangle-fill text-warning', class: modalClass = '', html = false, classDialog = '', classContent = '' } = options;
        await createModal(title, icon, text, [{ text: 'OK', class: 'btn-primary', value: true }], modalClass, html, 0, null, classDialog, classContent);
    };

    window.confirm = async function(text, options = {}) {
        const { title = 'Confirmation', icon = 'bi-question-circle-fill text-primary', class: modalClass = '', buttons = [
            { text: 'OK', class: 'btn-primary', value: true },
            { text: 'Cancel', class: 'btn-secondary', value: false }
        ], focus = 0, classDialog = '', classContent = '' } = options;
        const result = await createModal(title, icon, text, buttons, modalClass, false, focus, false, classDialog, classContent);
        try {
            return JSON.parse(result); // Convert value to boolean if possible
        } catch (e) {
            return result; // Return as-is if not JSON-parseable
        }
    };

    window.prompt = async function(text, defaultValue = '', options = {}) {
        const { title = 'Prompt', icon = 'bi-pencil-square text-secondary', class: modalClass = '', buttons = [
            { text: 'OK', class: 'btn-primary', value: 'ok' },
            { text: 'Cancel', class: 'btn-secondary', value: 'cancel' }
        ], focus = 0, classDialog = '', classContent = '' } = options;
        const content = `
            <div class="w-100">
                <label for="prompt-input" class="form-label">${text}</label>
                <input type="text" class="form-control mt-2" id="prompt-input" value="${defaultValue}">
            </div>`;
        const result = await createModal(title, icon, content, buttons, modalClass, true, focus, null, classDialog, classContent);
        if (result === 'cancel' || result === null) {
            return null;
        }
        const input = document.getElementById('prompt-input').value;
        return input;
    };

    // Focus and select input once the modal is opened
    document.addEventListener('shown.bs.modal', (event) => {
        const input = event.target.querySelector('input');
        if (input) {
            input.focus();
            input.select();
            input.addEventListener('keydown', (e) => {
                if (e.key === 'Enter') {
                    const firstButton = event.target.querySelector('.modal-footer .btn[data-index="0"]');
                    if (firstButton) {
                        firstButton.click();
                    }
                }
            });
        }
    });
})();
