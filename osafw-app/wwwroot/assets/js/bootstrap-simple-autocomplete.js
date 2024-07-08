/**
 * BootstrapSimpleAutocomplete Class
 * A simple and lightweight autocomplete component for Bootstrap 5.
 * https://github.com/osalabs/bootstrap-simple-autocomplete
 * (c) 2024-present Oleg Savchuk
 * @license MIT
 * 
 * Usage:
 * Include this script in your HTML and add the attribute 'data-autocomplete="URL"' to your input elements.
 * 
 * Example:
 * <input type="text" data-autocomplete="https://localhost?q=" class="form-control">
 * <script src="bootstrap-simple-autocomplete.js"></script>
 * 
 * ES6 Module Usage:
 * import { BootstrapSimpleAutocomplete, initializeAutocomplete } from 'bootstrap-simple-autocomplete.js';
 * 
 * Initialize:
 * initializeAutocomplete(); // To auto-initialize all elements with data-autocomplete attribute
 * or
 * new BootstrapSimpleAutocomplete(document.querySelector('input[data-autocomplete]')); // To initialize specific element
 */
class BootstrapSimpleAutocomplete {
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

    debounce(fn, delay) {
        let timeoutId;
        return function (...args) {
            clearTimeout(timeoutId);
            timeoutId = setTimeout(() => fn.apply(this, args), delay);
        };
    }

    addEventListeners() {
        this.input.addEventListener('input', this.onInput.bind(this));
        this.input.addEventListener('keydown', this.onKeyDown.bind(this));
        document.addEventListener('click', this.onClickOutside.bind(this), { passive: true });
    }

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

    onKeyDown(event) {
        const hasSuggestions = this.dropdown.querySelector('.dropdown-item');
        if ((event.key === 'Tab' || event.key === 'Enter') && hasSuggestions) {
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

    onClickOutside(event) {
        if (!this.dropdown.contains(event.target) && event.target !== this.input) {
            this.closeDropdown();
        }
    }

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

    clearPrefill() {
      const prefillSpan = this.input.parentNode.querySelector('.autocomplete-suggestion');
      if (prefillSpan) {
        prefillSpan.remove();
      }
    }

    selectOption(option) {
        this.input.value = option;
        this.closeDropdown();
    }

    closeDropdown() {
        this.dropdown.classList.remove('show');
        this.dropdown.innerHTML = '';
        this.clearPrefill();
    }

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

// Check if running as a module or script
if (typeof window !== 'undefined' && typeof document !== 'undefined') {
    document.addEventListener('DOMContentLoaded', () => {
        initializeAutocomplete();
    });
}

// Check if running as an ES module or CommonJS module
(function() {
    // ES module
    if (typeof window === 'undefined' && typeof document === 'undefined') {
        try {
            // Export for ES module
            export { BootstrapSimpleAutocomplete, initializeAutocomplete };
        } catch (e) {
            // Export for CommonJS module (Node.js)
            if (typeof module !== 'undefined' && module.exports) {
                module.exports = {
                    BootstrapSimpleAutocomplete,
                    initializeAutocomplete
                };
            }
        }
    }
})();