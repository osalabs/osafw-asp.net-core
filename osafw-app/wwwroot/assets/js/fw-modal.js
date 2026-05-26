(function () {
    if (document.documentElement.hasAttribute('data-fw-modal-inited')) return;
    document.documentElement.setAttribute('data-fw-modal-inited', '1');

    var modalCounter = 0;
    var triggerCounter = 0;
    var HTML_LOADING = '<div class="modal-body d-flex flex-column justify-content-center align-items-center text-center py-5 px-4" style="min-height: 14rem;">'
      + '<div class="spinner-border text-primary mb-3" role="status" aria-hidden="true"></div>'
      + '<div class="fw-semibold">Loading...</div>'
      + '</div>';

    function getFw() {
      return window.fw || null;
    }

    function getLoadingHtml() {
      var fw = getFw();
      return fw && fw.HTML_LOADING ? fw.HTML_LOADING : HTML_LOADING;
    }

    function showAlert(message) {
      var fw = getFw();
      if (fw && typeof fw.alert === 'function') {
        fw.alert(message);
        return;
      }
      window.alert(message);
    }

    function showToastError(message) {
      var fw = getFw();
      if (fw && typeof fw.error === 'function') {
        fw.error(message);
      }
    }

    function renderInlineError(container, message) {
      if (!container) return;

      var root = container.querySelector('.modal-body') || container;
      var alert = root.querySelector('.fw-modal-inline-error');
      if (!alert) {
        alert = document.createElement('div');
        alert.className = 'alert alert-danger fw-modal-inline-error';
        root.prepend(alert);
      }
      alert.textContent = message || 'An unexpected error occurred.';
    }

    function cleanFormErrors(form) {
      var fw = getFw();
      if (fw && typeof fw.clean_form_errors === 'function') {
        fw.clean_form_errors(form);
      }
    }

    function processFormErrors(form, details) {
      var fw = getFw();
      if (fw && typeof fw.process_form_errors === 'function') {
        fw.process_form_errors(form, details);
      }
    }

    function ensureTriggerId(trigger) {
      var triggerId = trigger.getAttribute('data-fw-modal-trigger-id');
      if (!triggerId) {
        triggerCounter += 1;
        triggerId = 'fw-modal-trigger-' + Date.now() + '-' + triggerCounter;
        trigger.setAttribute('data-fw-modal-trigger-id', triggerId);
      }
      return triggerId;
    }

    function resolveLookupTarget(trigger) {
      var targetSelector = trigger.getAttribute('data-fw-lookup-target');
      if (targetSelector) {
        var explicitTarget = document.querySelector(targetSelector);
        if (explicitTarget) return explicitTarget;
      }

      var group = trigger.closest('.input-group');
      if (!group) return null;
      return group.querySelector('select, input, textarea');
    }

    function buildLookupContext(trigger) {
      var lookupMode = trigger.getAttribute('data-fw-lookup');
      if (!lookupMode) return null;

      return {
        mode: lookupMode,
        target: resolveLookupTarget(trigger),
        trigger: trigger
      };
    }

    function buildLookupUrl(trigger, lookup) {
      var url = trigger.getAttribute('data-url') || trigger.getAttribute('href');
      if (!url) return url;

      if (url.indexOf('{id}') !== -1 || url.indexOf('{value}') !== -1) {
        var value = lookup && lookup.target ? lookup.target.value : '';
        if (!value) {
          showAlert('Select a value first');
          return null;
        }
        var encodedValue = encodeURIComponent(value);
        url = url.replace(/\{id\}/g, encodedValue);
        url = url.replace(/\{value\}/g, encodedValue);
      }

      return url;
    }

    function ensureModalLayout(url) {
      if (!url) return url;
      var parsedUrl = new URL(url, window.location.href);
      if (!parsedUrl.searchParams.has('_layout')) {
        parsedUrl.searchParams.set('_layout', 'modal');
      }
      return parsedUrl.toString();
    }

    function disposeModalContent(scope) {
      var fw = getFw();
      if (fw && typeof fw.disposeComponents === 'function') {
        fw.disposeComponents(scope);
      }
    }

    function copyBootstrapAttributes(trigger, modal) {
      var excludedAttributes = {
        'data-bs-toggle': true,
        'data-bs-target': true,
        'data-bs-dismiss': true
      };

      Array.from(trigger.attributes).forEach(function (attr) {
        if (attr.name.indexOf('data-bs-') !== 0) return;
        if (excludedAttributes[attr.name]) return;
        modal.setAttribute(attr.name, attr.value);
      });
    }

    function buildModal(trigger) {
      modalCounter += 1;

      var modalId = 'fw-modal-' + Date.now() + '-' + modalCounter;
      var triggerId = ensureTriggerId(trigger);
      var dialogClass = trigger.getAttribute('data-modal-dialog-class') || 'modal-dialog modal-lg modal-dialog-scrollable';
      var contentClass = trigger.getAttribute('data-modal-content-class') || 'modal-content bg-body';
      var modalClass = trigger.getAttribute('data-modal-class') || '';

      var modal = document.createElement('div');
      modal.className = 'modal fade fw-modal';
      if (modalClass) {
        modal.className += ' ' + modalClass;
      }
      modal.tabIndex = -1;
      modal.setAttribute('role', 'dialog');
      modal.setAttribute('aria-hidden', 'true');
      modal.id = modalId;
      modal.setAttribute('data-fw-modal-trigger-id', triggerId);
      copyBootstrapAttributes(trigger, modal);

      var dialog = document.createElement('div');
      dialog.className = dialogClass;

      var content = document.createElement('div');
      content.className = contentClass;

      dialog.appendChild(content);
      modal.appendChild(dialog);
      document.body.appendChild(modal);

      modal.addEventListener('hidden.bs.modal', function () {
        var instance = typeof bootstrap !== 'undefined' && bootstrap.Modal
          ? bootstrap.Modal.getInstance(modal)
          : null;
        if (instance && typeof instance.dispose === 'function') {
          instance.dispose();
        }
        disposeModalContent(modal);
        modal.remove();
      });

      return modal;
    }

    function executeModalScripts(container) {
      Array.from(container.querySelectorAll('script')).forEach(function (oldScript) {
        var newScript = document.createElement('script');

        Array.from(oldScript.attributes).forEach(function (attr) {
          newScript.setAttribute(attr.name, attr.value);
        });

        if (oldScript.src) {
          newScript.src = oldScript.src;
          newScript.async = false;
        } else {
          newScript.textContent = oldScript.textContent;
        }

        oldScript.parentNode.replaceChild(newScript, oldScript);
      });
    }

    function namespaceModalContentIds(modal, container) {
      if (!modal || !container || modal.dataset.fwModalNamespaceIds !== '1') return;

      var idMap = {};
      Array.from(container.querySelectorAll('[id]')).forEach(function (el) {
        if (el.getAttribute('data-fw-keep-id') === '1') return;

        var originalId = el.getAttribute('id');
        if (!originalId) return;

        var namespacedId = modal.id + '-' + originalId;
        idMap[originalId] = namespacedId;
        el.setAttribute('data-fw-original-id', originalId);
        el.setAttribute('id', namespacedId);
      });

      if (!Object.keys(idMap).length) return;

      ['for', 'form', 'list'].forEach(function (attrName) {
        Array.from(container.querySelectorAll('[' + attrName + ']')).forEach(function (el) {
          var value = el.getAttribute(attrName);
          if (idMap[value]) {
            el.setAttribute(attrName, idMap[value]);
          }
        });
      });

      ['aria-controls', 'aria-describedby', 'aria-labelledby', 'aria-owns', 'aria-activedescendant'].forEach(function (attrName) {
        Array.from(container.querySelectorAll('[' + attrName + ']')).forEach(function (el) {
          el.setAttribute(attrName, (el.getAttribute(attrName) || '').split(/\s+/).map(function (id) {
            return idMap[id] || id;
          }).join(' '));
        });
      });

      ['data-bs-target', 'data-target'].forEach(function (attrName) {
        Array.from(container.querySelectorAll('[' + attrName + ']')).forEach(function (el) {
          var value = el.getAttribute(attrName);
          if (value && value.charAt(0) === '#' && idMap[value.slice(1)]) {
            el.setAttribute(attrName, '#' + idMap[value.slice(1)]);
          }
        });
      });

      Array.from(container.querySelectorAll('a[href^="#"], area[href^="#"]')).forEach(function (el) {
        var value = el.getAttribute('href');
        if (value && idMap[value.slice(1)]) {
          el.setAttribute('href', '#' + idMap[value.slice(1)]);
        }
      });
    }

    function setModalContent(modal, html) {
      var content = modal.querySelector('.modal-content');
      if (!content) return;
      disposeModalContent(content);
      content.innerHTML = html;
      namespaceModalContentIds(modal, content);
      executeModalScripts(content);
    }

    function escapeHtml(message) {
      var div = document.createElement('div');
      div.textContent = message == null ? '' : String(message);
      return div.innerHTML;
    }

    function showLoadError(modal, message) {
      var safeMessage = escapeHtml(message || '');
      var html = '<div class="modal-body"><div class="alert alert-danger mb-0">' + safeMessage + '</div></div>';
      setModalContent(modal, html);
      showToastError(message || 'Failed to load modal content.');
    }

    async function fetchText(url, init) {
      var response = await fetch(url, init);
      var text = await response.text();
      return { response: response, text: text };
    }

    async function fetchJson(url, init) {
      var response = await fetch(url, init);
      var text = await response.text();
      var data = null;

      if (text) {
        try {
          data = JSON.parse(text);
        } catch (err) {
          data = null;
        }
      }

      return { response: response, text: text, data: data };
    }

    async function loadModalContent(modal, url) {
      var modalUrl = ensureModalLayout(url);
      var content = modal.querySelector('.modal-content');
      modal.dataset.fwModalUrl = modalUrl;
      if (content) {
        disposeModalContent(content);
        content.innerHTML = getLoadingHtml();
      }

      try {
        var result = await fetchText(modalUrl, {
          method: 'GET',
          credentials: 'same-origin',
          headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (!result.response.ok) {
          renderHtmlResultOrError(modal, result, 'Failed to load modal content.');
          return;
        }

        setModalContent(modal, result.text);
      } catch (err) {
        showLoadError(modal, err && err.message ? err.message : 'Failed to load modal content.');
      }
    }

    function appendSubmitterValue(collection, submitter) {
      if (!submitter || !submitter.name) return;
      collection.append(submitter.name, submitter.value || '');
    }

    function ensureCollectionValue(collection, key, value) {
      if (!collection.has(key)) {
        collection.append(key, value);
      } else {
        collection.set(key, value);
      }
    }

    function getSubmitterOverride(submitter, attrName) {
      if (!submitter || typeof submitter.getAttribute !== 'function') return '';
      return submitter.getAttribute(attrName) || '';
    }

    function resolveModalSubmitForm(modal, submitter) {
      if (!modal || !submitter) return null;

      var target = submitter.getAttribute('data-target');
      if (target === 'modal') {
        return modal.querySelector('form');
      }

      if (target) {
        try {
          var scopedTarget = modal.querySelector(target);
          if (scopedTarget instanceof HTMLFormElement) return scopedTarget;
        } catch (err) {
          return null;
        }
      }

      if (submitter.form instanceof HTMLFormElement) return submitter.form;
      return submitter.closest('form');
    }

    function isHtmlResponse(result) {
      if (!result || !result.response) return false;

      var contentType = result.response.headers.get('content-type') || '';
      if (contentType.toLowerCase().indexOf('text/html') >= 0) return true;

      var text = (result.text || '').trim().toLowerCase();
      if (!text) return false;

      return text.indexOf('<!doctype html') === 0
        || text.indexOf('<html') === 0
        || text.indexOf('<div') === 0
        || text.indexOf('<section') === 0;
    }

    function renderHtmlResultOrError(modal, result, fallbackMessage) {
      if (isHtmlResponse(result) && result.text) {
        setModalContent(modal, result.text);
        return true;
      }

      showLoadError(modal, result && result.text ? result.text : fallbackMessage);
      return false;
    }

    function startSubmitterSpinner(submitter) {
      if (!(submitter instanceof HTMLElement)) return null;
      if (!submitter.hasAttribute('data-spinner')) return null;
      if (submitter._fwModalSpinnerState) return submitter._fwModalSpinnerState;

      var state = {
        disabled: !!submitter.disabled,
        hiddenIcons: [],
        spinner: null
      };

      submitter.disabled = true;

      var iconNodes = Array.from(submitter.querySelectorAll('i, .bi, .icon'));
      iconNodes.forEach(function (node) {
        if (!(node instanceof HTMLElement)) return;
        if (node.classList.contains('spinner-border')) return;
        if (node.classList.contains('spinner-grow')) return;
        state.hiddenIcons.push({
          node: node,
          className: node.className
        });
        node.classList.add('d-none');
      });

      var spinner = document.createElement('span');
      spinner.className = 'spinner-border spinner-border-sm';
      spinner.setAttribute('aria-hidden', 'true');

      var textContent = (submitter.textContent || '').trim();
      if (textContent) {
        spinner.classList.add('me-2');
      }

      submitter.prepend(spinner);
      state.spinner = spinner;
      submitter._fwModalSpinnerState = state;
      return state;
    }

    function stopSubmitterSpinner(submitter) {
      if (!(submitter instanceof HTMLElement)) return;

      var state = submitter._fwModalSpinnerState;
      if (!state) return;

      if (state.spinner && state.spinner.parentNode) {
        state.spinner.remove();
      }

      state.hiddenIcons.forEach(function (item) {
        if (!item.node || !item.node.classList) return;
        item.node.className = item.className;
      });

      submitter.disabled = state.disabled;
      delete submitter._fwModalSpinnerState;
    }

    function buildFormRequest(form, submitter) {
      var submitterMethod = getSubmitterOverride(submitter, 'formmethod');
      var submitterAction = getSubmitterOverride(submitter, 'formaction');
      var method = (submitterMethod || form.getAttribute('method') || form.method || 'GET').toUpperCase();
      var action = submitterAction || form.getAttribute('action') || form.action || window.location.href;
      var url = new URL(action, window.location.href);
      var headers = { 'X-Requested-With': 'XMLHttpRequest' };

      if (method === 'GET') {
        var params = new URLSearchParams(new FormData(form));
        appendSubmitterValue(params, submitter);
        ensureCollectionValue(params, '_layout', 'modal');
        params.forEach(function (value, key) {
          url.searchParams.set(key, value);
        });

        return {
          url: ensureModalLayout(url.toString()),
          init: {
            method: 'GET',
            credentials: 'same-origin',
            headers: headers
          }
        };
      }

      var formData = new FormData(form);
      appendSubmitterValue(formData, submitter);
      ensureCollectionValue(formData, '_layout', 'modal');

      return {
        url: ensureModalLayout(url.toString()),
        init: {
          method: method,
          body: formData,
          credentials: 'same-origin',
          headers: headers
        },
        formData: formData
      };
    }

    function updateLookupTarget(lookup, data, form, modal) {
      if (!lookup || !lookup.target || !data || data.id == null || data.id === '') return;

      var idValue = String(data.id);
      var label = data.lookup_label || '';
      if (!label) {
        var inameField = form.querySelector('[name="item[iname]"]');
        label = inameField ? inameField.value : '';
      }

      var option = null;
      if (lookup.target.tagName === 'SELECT') {
        option = Array.from(lookup.target.options).find(function (oneOption) {
          return oneOption.value === idValue;
        });

        if (!option) {
          option = document.createElement('option');
          option.value = idValue;
          lookup.target.appendChild(option);
        }

        if (label) {
          option.textContent = label;
        }

        option.selected = true;
        lookup.target.value = idValue;
      } else {
        lookup.target.value = idValue;
      }

      if (window.jQuery) {
        var $target = jQuery(lookup.target);
        if ($target.hasClass('selectpicker') && jQuery.fn.selectpicker) {
          $target.selectpicker('refresh');
        }
        if ($target.data('select2') && jQuery.fn.select2) {
          $target.trigger('change.select2');
        }
      }

      lookup.target.dispatchEvent(new CustomEvent('fw-lookup-saved', {
        bubbles: true,
        detail: {
          mode: lookup.mode,
          id: idValue,
          value: idValue,
          label: label,
          data: data,
          option: option || null,
          target: lookup.target,
          trigger: lookup.trigger || null,
          modal: modal,
          form: form
        }
      }));
      lookup.target.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function hideModal(modal) {
      if (typeof bootstrap === 'undefined' || !bootstrap.Modal) return;
      var instance = bootstrap.Modal.getInstance(modal);
      if (instance) {
        instance.hide();
      }
    }

    async function submitLookupForm(modal, form, submitter, lookup) {
      startSubmitterSpinner(submitter);
      cleanFormErrors(form);
      Array.from(form.querySelectorAll('.fw-modal-inline-error')).forEach(function (el) {
        el.remove();
      });
      var request = buildFormRequest(form, submitter);

      if (request.formData) {
        ensureCollectionValue(request.formData, 'lookup', lookup.mode || '1');
      } else {
        var requestUrl = new URL(request.url, window.location.href);
        requestUrl.searchParams.set('lookup', lookup.mode || '1');
        request.url = requestUrl.toString();
      }

      request.init.headers = Object.assign({}, request.init.headers, { Accept: 'application/json' });

      try {
        var result = await fetchJson(request.url, request.init);
        var data = result.data || {};
        var error = data && data.error ? data.error : null;

        if (!result.response.ok && isHtmlResponse(result)) {
          renderHtmlResultOrError(modal, result, 'Failed to submit form.');
          return;
        }

        if (!result.response.ok || error) {
          cleanFormErrors(form);
          if (error && error.details) {
            processFormErrors(form, error.details);
          }
          var message = error && error.message
            ? error.message
            : (result.text || 'Failed to submit form.');
          renderInlineError(form, message);
          showToastError(message);
          return;
        }

        updateLookupTarget(lookup, data, form, modal);
        hideModal(modal);
      } catch (err) {
        var fallbackMessage = err && err.message ? err.message : 'Failed to submit form.';
        renderInlineError(form, fallbackMessage);
        showToastError(fallbackMessage);
      } finally {
        stopSubmitterSpinner(submitter);
      }
    }

    async function submitHtmlForm(modal, form, submitter) {
      startSubmitterSpinner(submitter);
      Array.from(form.querySelectorAll('.fw-modal-inline-error')).forEach(function (el) {
        el.remove();
      });
      var request = buildFormRequest(form, submitter);

      try {
        var result = await fetchText(request.url, request.init);
        if (!result.response.ok) {
          renderHtmlResultOrError(modal, result, 'Failed to submit form.');
          return;
        }
        setModalContent(modal, result.text);
      } catch (err) {
        showLoadError(modal, err && err.message ? err.message : 'Failed to submit form.');
      } finally {
        stopSubmitterSpinner(submitter);
      }
    }

    function shouldLoadLinkInModal(e, trigger, url) {
      if (!url || e.defaultPrevented) return false;
      if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return false;
      if (trigger.hasAttribute('download')) return false;

      var target = trigger.getAttribute('target');
      if (target && target.toLowerCase() !== '_self') return false;
      if (url.charAt(0) === '#') return false;

      try {
        var parsedUrl = new URL(url, window.location.href);
        if (parsedUrl.protocol !== 'http:' && parsedUrl.protocol !== 'https:') return false;
        return parsedUrl.origin === window.location.origin;
      } catch (err) {
        return false;
      }
    }

    document.addEventListener('click', function (e) {
      var trigger = e.target.closest('.on-fw-modal-link');
      if (!trigger) return;

      var modal = trigger.closest('.fw-modal');
      if (!modal) return;

      var url = trigger.getAttribute('data-url') || trigger.getAttribute('href');
      if (!shouldLoadLinkInModal(e, trigger, url)) return;

      e.preventDefault();
      loadModalContent(modal, url);
    });

    document.addEventListener('click', function (e) {
      var submitter = e.target.closest('.fw-modal .on-fw-modal-submit, .fw-modal .on-submit[data-target="modal"]');
      if (!submitter) return;

      var modal = submitter.closest('.fw-modal');
      var form = resolveModalSubmitForm(modal, submitter);
      if (!modal || !form || !modal.contains(form)) return;

      e.preventDefault();
      e.stopImmediatePropagation();

      if (submitter.hasAttribute('data-refresh')) {
        var refreshInput = Array.from(form.elements).find(function (el) {
          return el.name === 'refresh';
        });
        if (refreshInput) {
          refreshInput.value = '1';
        }
      }

      var buttonName = submitter.getAttribute('name');
      if (buttonName) {
        var buttonInput = Array.from(form.elements).find(function (el) {
          return el.name === buttonName;
        });
        if (!buttonInput) {
          buttonInput = document.createElement('input');
          buttonInput.type = 'hidden';
          buttonInput.name = buttonName;
          form.appendChild(buttonInput);
        }
        buttonInput.value = submitter.getAttribute('value') || '';
      }

      var lookup = modal._fwLookup || null;
      if (lookup) {
        submitLookupForm(modal, form, submitter, lookup);
        return;
      }

      submitHtmlForm(modal, form, submitter);
    }, true);

    document.addEventListener('click', function (e) {
      var trigger = e.target.closest('.on-fw-modal');
      if (!trigger) return;

      var urlAttr = trigger.getAttribute('data-url') || trigger.getAttribute('href');
      if (!urlAttr) return;

      e.preventDefault();

      var lookup = buildLookupContext(trigger);
      var url = buildLookupUrl(trigger, lookup);
      if (!url) return;

      var modal = buildModal(trigger);
      var namespaceAttr = String(trigger.getAttribute('data-fw-modal-namespace-ids') || '').toLowerCase();
      modal.dataset.fwModalNamespaceIds = namespaceAttr
        ? (['1', 'true', 'yes'].indexOf(namespaceAttr) >= 0 ? '1' : '0')
        : (lookup ? '1' : '0');
      if (lookup) {
        modal._fwLookup = lookup;
      }

      loadModalContent(modal, url);

      if (typeof bootstrap === 'undefined' || !bootstrap.Modal) {
        throw new Error('Bootstrap Modal is required for .on-fw-modal triggers.');
      }

      var instance = new bootstrap.Modal(modal);
      instance.show();
    });

    document.addEventListener('submit', function (e) {
      var form = e.target;
      if (!(form instanceof HTMLFormElement)) return;

      var modal = form.closest('.fw-modal');
      if (!modal) return;

      e.preventDefault();

      var lookup = modal._fwLookup || null;
      if (lookup) {
        submitLookupForm(modal, form, e.submitter || null, lookup);
        return;
      }

      submitHtmlForm(modal, form, e.submitter || null);
    });
  })();

