/**
 * BootstrapSimpleAutocomplete Class
 * A simple and lightweight autocomplete component for Bootstrap 5.
 * 
 * Usage:
 * Include this script in your HTML and add the attribute 'data-autocomplete="URL"' to your input elements.
 * 
 * Example:
 * <input type="text" data-autocomplete="https://localhost?q=" class="form-control">
 * 
 * ES6 Module Usage:
 * import { BootstrapSimpleAutocomplete, initializeAutocomplete } from './path_to/bootstrap-simple-autocomplete.js';
 * 
 * Initialize:
 * initializeAutocomplete(); // To auto-initialize all elements with data-autocomplete attribute
 * or
 * new BootstrapSimpleAutocomplete(document.querySelector('input[data-autocomplete]')); // To initialize specific element
 */
class BootstrapSimpleAutocomplete {
    /**
     * Constructor to initialize the component.
     * @param {HTMLInputElement} input - The input element to attach the autocomplete to.
     */
    constructor(input) {
        this.input = input;
        this.url = input.getAttribute('data-autocomplete');

        // Create wrapper and dropdown elements
        const wrapper = document.createElement('div');
        wrapper.className = 'position-relative';
        input.parentNode.insertBefore(wrapper, input);
        wrapper.appendChild(input);

        this.dropdown = document.createElement('div');
        this.dropdown.className = 'dropdown-menu';
        this.dropdown.style.maxHeight = '224px'; // Limit the height to show 7 items (each item ~32px height)
        this.dropdown.style.overflowY = 'auto';  // Make it scrollable
        wrapper.appendChild(this.dropdown);

        // Create debounced version of fetchData
        this.debouncedFetchData = this.debounce(this.fetchData.bind(this), 300);

        this.addEventListeners();
        this.insertStyles();
    }

    /**
     * Debounce function to limit the rate of function calls.
     * @param {Function} fn - The function to debounce.
     * @param {number} delay - The debounce delay in milliseconds.
     * @returns {Function} - The debounced function.
     */
    debounce(fn, delay) {
        let timeoutId;
        return function (...args) {
            clearTimeout(timeoutId);
            timeoutId = setTimeout(() => fn.apply(this, args), delay);
        };
    }

    /**
     * Add event listeners to the input element and document.
     */
    addEventListeners() {
        this.input.addEventListener('input', this.onInput.bind(this));
        this.input.addEventListener('keydown', this.onKeyDown.bind(this));
        document.addEventListener('click', this.onClickOutside.bind(this), { passive: true });
    }

    /**
     * Handle input event to fetch suggestions.
     * @param {Event} event - The input event.
     */
    onInput(event) {
        const query = event.target.value;
        if (query.length === 0) {
            this.closeDropdown();
            return;
        }

        // Prefill input immediately
        const firstOption = this.dropdown.querySelector('.dropdown-item');
        if (firstOption && firstOption.textContent.toLowerCase().startsWith(query.toLowerCase())) {
            this.prefillInput(firstOption.textContent, query);
        } else {
            this.clearPrefill();
        }

        this.debouncedFetchData(query);
    }

    /**
     * Fetch suggestions from the server.
     * @param {string} query - The query string.
     */
    fetchData(query) {
        fetch(`${this.url}${encodeURIComponent(query)}`, {
            headers: {
                'Accept': 'application/json'
            }
        })
            .then(response => response.json())
            .then(data => this.showDropdown(data, query))
            .catch(err => this.showError(err));
    }

    /**
     * Handle keydown event for navigation and selection.
     * @param {Event} event - The keydown event.
     */
    onKeyDown(event) {
        if (event.key === 'Tab' || event.key === 'Enter') {
            event.preventDefault();
            const activeOption = this.dropdown.querySelector('.dropdown-item.active');
            if (activeOption) {
                this.selectOption(activeOption.textContent);
            } else {
                const firstOption = this.dropdown.querySelector('.dropdown-item');
                if (firstOption) {
                    this.selectOption(firstOption.textContent);
                }
            }
        } else if (event.key === 'Escape') {
            this.closeDropdown();
        } else if (event.key === 'ArrowDown') {
            event.preventDefault(); // Prevent cursor movement
            this.navigateDropdown('down');
        } else if (event.key === 'ArrowUp') {
            event.preventDefault(); // Prevent cursor movement
            this.navigateDropdown('up');
        }
    }

    /**
     * Handle click event outside the dropdown to close it.
     * @param {Event} event - The click event.
     */
    onClickOutside(event) {
        if (!this.dropdown.contains(event.target) && event.target !== this.input) {
            this.closeDropdown();
        }
    }

    /**
     * Show the dropdown with suggestions.
     * @param {Array<string>} options - The list of suggestions.
     * @param {string} query - The query string.
     */
    showDropdown(options, query) {
        this.dropdown.innerHTML = '';
        if (options.length > 0) {
            const fragment = document.createDocumentFragment();
            const firstOption = options[0];
            if (firstOption.toLowerCase().startsWith(query.toLowerCase())) {
                this.prefillInput(firstOption, query);
            }
            options.forEach(option => {
                const item = document.createElement('a');
                item.className = 'dropdown-item';
                item.setAttribute('role', 'option');
                item.setAttribute('aria-selected', 'false');
                item.textContent = option;
                item.addEventListener('click', () => this.selectOption(option));
                fragment.appendChild(item);
            });
            this.dropdown.appendChild(fragment);
            this.dropdown.classList.add('show');
        } else {
            this.closeDropdown();
        }
    }

    /**
     * Prefill the input with the first suggestion.
     * @param {string} option - The suggestion text.
     * @param {string} query - The query string.
     */
    prefillInput(option, query) {
        const prefillSpan = this.input.parentNode.querySelector('.autocomplete-suggestion');
        if (prefillSpan) {
            prefillSpan.textContent = query + option.slice(query.length);
        } else {
            const newPrefillSpan = document.createElement('span');
            newPrefillSpan.className = 'autocomplete-suggestion';
            newPrefillSpan.textContent = query + option.slice(query.length);
            this.input.parentNode.appendChild(newPrefillSpan);

            // Adjust the position and size to match input
            const inputRect = this.input.getBoundingClientRect();
            const inputStyles = window.getComputedStyle(this.input);
            newPrefillSpan.style.height = `${inputRect.height}px`;
            newPrefillSpan.style.width = `${inputRect.width}px`;            
            newPrefillSpan.style.fontSize = inputStyles.fontSize;
            newPrefillSpan.style.fontFamily = inputStyles.fontFamily;
            newPrefillSpan.style.fontWeight = inputStyles.fontWeight;
            newPrefillSpan.style.fontStyle = inputStyles.fontStyle;
            newPrefillSpan.style.color = inputStyles.color;
            // Set padding from input but also add input border size to padding sizes
            newPrefillSpan.style.paddingLeft = (parseInt(inputStyles.paddingLeft) + parseInt(inputStyles.borderLeftWidth)) + 'px';
            newPrefillSpan.style.paddingTop = (parseInt(inputStyles.paddingTop) + parseInt(inputStyles.borderTopWidth)) + 'px';
            newPrefillSpan.style.paddingRight = (parseInt(inputStyles.paddingRight) + parseInt(inputStyles.borderRightWidth)) + 'px';
            newPrefillSpan.style.paddingBottom = (parseInt(inputStyles.paddingBottom) + parseInt(inputStyles.borderBottomWidth)) + 'px';
        }
    }

    /**
     * Clear the prefill suggestion.
     */
    clearPrefill() {
        const prefillSpan = this.input.parentNode.querySelector('.autocomplete-suggestion');
        if (prefillSpan) {
            prefillSpan.remove();
        }
    }

    /**
     * Select a suggestion and update the input value.
     * @param {string} option - The selected suggestion.
     */
    selectOption(option) {
        this.input.value = option;
        this.closeDropdown();
    }

    /**
     * Close the dropdown and clear suggestions.
     */
    closeDropdown() {
        this.dropdown.classList.remove('show');
        this.dropdown.innerHTML = '';
        this.clearPrefill();
    }

    /**
     * Show an error message in the dropdown.
     * @param {Error} err - The error object.
     */
    showError(err) {
        console.error(err);
        this.dropdown.innerHTML = '';
        const error = document.createElement('div');
        error.className = 'dropdown-item text-danger';
        error.textContent = 'Error fetching results';
        error.style.pointerEvents = 'none';
        this.dropdown.appendChild(error);
        this.dropdown.classList.add('show');
    }

    /**
     * Navigate the dropdown options using arrow keys.
     * @param {string} direction - The direction of navigation ('up' or 'down').
     */
    navigateDropdown(direction) {
        const items = Array.from(this.dropdown.querySelectorAll('.dropdown-item'));
        if (items.length === 0) return;
        let activeIndex = items.findIndex(item => item.classList.contains('active'));
        if (direction === 'down') {
            activeIndex = (activeIndex + 1) % items.length;
        } else {
            activeIndex = (activeIndex - 1 + items.length) % items.length;
        }
        items.forEach((item, index) => {
            if (index === activeIndex) {
                item.classList.add('active');
                item.scrollIntoView({ block: 'nearest' });
                item.setAttribute('aria-selected', 'true');
                this.prefillInput(item.textContent, this.input.value);
            } else {
                item.classList.remove('active');
                item.setAttribute('aria-selected', 'false');
            }
        });
    }

    /**
     * Insert necessary styles into the document head.
     */
    insertStyles() {
        const style = document.createElement('style');
        style.innerHTML = `
            .autocomplete-suggestion {
                position: absolute;
                left: 0;
                top: 0;
                z-index: 10;
                pointer-events: none;
                opacity: 0.5;
            }
            .dropdown-item.active {
                background-color: #e9ecef;
            }
        `;
        document.head.appendChild(style);
    }
}

/**
 * Initialize all autocomplete inputs on the page.
 */
function initializeAutocomplete() {
    const inputs = document.querySelectorAll('input[data-autocomplete]');
    inputs.forEach(input => new BootstrapSimpleAutocomplete(input));
}

// Export the class and initialization function
export { BootstrapSimpleAutocomplete, initializeAutocomplete };
