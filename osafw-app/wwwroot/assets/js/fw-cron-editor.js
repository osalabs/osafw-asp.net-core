(function () {
  'use strict';

  function includeSelf(scope, selector) {
    const children = Array.from(scope.querySelectorAll(selector));
    if (scope.matches && scope.matches(selector)) children.unshift(scope);
    return children;
  }

  function preset(expression) {
    const fields = expression.trim().split(/\s+/);
    if (fields.length !== 5 || fields[3] !== '*' ||
        !/^(?:[0-9]|[1-5][0-9])$/.test(fields[0]) ||
        !/^(?:[0-9]|1[0-9]|2[0-3])$/.test(fields[1])) return null;

    const day = fields[2];
    const weekdays = fields[4];
    const time = fields[1].padStart(2, '0') + ':' + fields[0].padStart(2, '0');
    if (day === '*' && weekdays === '*') return {mode: 'daily', time};
    if (day === '*' && weekdays === '1-5') return {mode: 'weekdays', time};
    if (day === '*' && /^[0-6](?:,[0-6])*$/.test(weekdays)) {
      const days = weekdays.split(',');
      if (new Set(days).size === days.length) return {mode: 'weekly', time, days};
    }
    if (/^(?:[1-9]|[12][0-9]|3[01])$/.test(day) && weekdays === '*')
      return {mode: 'monthly', time, day};

    return null;
  }

  function setupEditor(root) {
    if (root.dataset.cronInitialized) return;
    root.dataset.cronInitialized = '1';
    const find = key => root.querySelector('[data-cron-' + key + ']');
    const expression = find('expression');
    const mode = find('mode');
    const time = find('time');
    const zone = find('zone');
    const day = find('day');
    const weekdays = Array.from(root.querySelectorAll('[data-cron-weekday]'));
    const previewRoot = document.getElementById(root.dataset.previewId) || root;
    const message = previewRoot.querySelector('[data-cron-preview-message]');
    const occurrences = previewRoot.querySelector('[data-cron-preview]');
    const previewTable = previewRoot.querySelector('[data-cron-preview-table]');
    const taskZone = previewRoot.querySelector('[data-cron-task-zone]');
    const editors = Array.from(root.querySelectorAll('[data-cron-edit-mode]'));
    let isAdvanced;
    const form = root.closest('form');
    let pending;
    let debounce;

    const initial = preset(expression.value);
    isAdvanced = !initial;
    if (initial) {
      mode.value = initial.mode;
      time.value = initial.time;
      if (initial.day) day.value = initial.day;
      if (initial.days) weekdays.forEach(box => box.checked = initial.days.includes(box.value));
    }

    function updateExpression() {
      const kind = mode.value;
      find('presets').hidden = isAdvanced;
      editors.forEach(button => {
        const active = (button.dataset.cronEditMode === 'expression') === isAdvanced;
        button.classList.toggle('active', active);
        button.setAttribute('aria-pressed', String(active));
      });
      find('expression-group').hidden = !isAdvanced;
      find('time-group').hidden = isAdvanced;
      find('week-group').hidden = isAdvanced || kind !== 'weekly';
      find('month-group').hidden = isAdvanced || kind !== 'monthly';
      expression.required = isAdvanced;
      time.required = !isAdvanced;
      day.disabled = isAdvanced || kind !== 'monthly';
      day.required = !isAdvanced && kind === 'monthly';
      const days = weekdays.filter(box => box.checked).map(box => box.value);
      mode.setCustomValidity(!isAdvanced && kind === 'weekly' && !days.length ? 'Select at least one weekday.' : '');
      if (isAdvanced) return;

      const [hour, minute] = time.value.split(':').map(Number);
      if (!time.value || !day.validity.valid || !mode.validity.valid) {
        expression.value = '';
        return;
      }

      const monthDay = kind === 'monthly' ? day.value : '*';
      const weekDay = kind === 'weekly' ? days.join(',') : kind === 'weekdays' ? '1-5' : '*';
      expression.value = [minute, hour, monthDay, '*', weekDay].join(' ');
    }

    async function preview() {
      if (pending) pending.abort();
      const request = new AbortController();
      pending = request;
      occurrences.replaceChildren();
      previewTable.hidden = true;
      taskZone.title = zone.value;
      if (!expression.value) {
        message.textContent = 'Complete the schedule to see upcoming occurrences.';
        return;
      }

      const url = new URL(root.dataset.previewUrl, document.baseURI);
      if (Number(root.dataset.taskId) > 0) url.searchParams.set('task_id', root.dataset.taskId);
      url.searchParams.set('item[cron]', expression.value);
      url.searchParams.set('item[timezone]', zone.value);
      if (form) {
        form.querySelectorAll('[data-cron-bound]').forEach(bound => {
          url.searchParams.set('item[' + bound.dataset.cronBound + ']', bound.value);
        });
      }

      try {
        const response = await fetch(url, {headers: {Accept: 'application/json'}, signal: request.signal});
        const result = await response.json();
        if (request.signal.aborted) return;
        if (!response.ok || !Array.isArray(result.occurrences)) {
          message.textContent = result.error || 'Preview unavailable.';
          return;
        }

        message.textContent = result.occurrences.length ? '' : 'No future occurrences fit this schedule.';
        previewTable.hidden = result.occurrences.length === 0;
        result.occurrences.forEach(occurrence => {
          const row = document.createElement('tr');
          for (const key of ['recurrence', 'local', 'utc']) {
            const cell = document.createElement('td');
            cell.textContent = occurrence[key];
            cell.title = occurrence[key + '_title'] || 'UTC (+00:00)';
            if (key === 'utc') cell.className = 'text-muted';
            row.append(cell);
          }
          occurrences.append(row);
        });
      } catch (error) {
        if (!request.signal.aborted) message.textContent = 'Preview unavailable.';
      }
    }

    function refresh(event) {
      if (!event || event.target !== zone) updateExpression();
      if (pending) pending.abort();
      clearTimeout(debounce);
      debounce = setTimeout(preview, 150);
    }

    find('editor-modes').hidden = false;
    editors.forEach(button => button.addEventListener('click', () => {
      isAdvanced = button.dataset.cronEditMode === 'expression';
      if (!isAdvanced) {
        const current = preset(expression.value);
        if (current) {
          mode.value = current.mode;
          time.value = current.time;
          day.value = current.day || '1';
          weekdays.forEach(box => box.checked = (current.days || []).includes(box.value));
        }
      }
      refresh();
    }));
    root.addEventListener('input', refresh);
    root.addEventListener('change', refresh);
    if (form) {
      form.querySelectorAll('[data-cron-bound]').forEach(bound => bound.addEventListener('change', refresh));
    }
    refresh();
  }

  function setupReport(scope) {
    if (scope.dataset.scheduleInitialized) return;
    const filter = scope.querySelector('form[data-schedule-filter]');
    const panel = scope.querySelector('form[data-report-schedule]');
    if (!filter || !panel) return;
    scope.dataset.scheduleInitialized = '1';

    const open = filter.querySelector('[data-schedule-open]');
    const cancel = panel.querySelector('[data-schedule-cancel]');
    const submit = panel.querySelector('button[type="submit"]');
    const summary = panel.querySelector('[data-schedule-summary]');
    const autosubmit = filter.hasAttribute('data-autosubmit');
    const hiddenNames = new Set(['format', 'pagesize', 'pagenum', 'to_emails', 'email_as']);

    function selectedFilters() {
      const entries = [];
      for (const [key, value] of new FormData(filter)) {
        const match = /^f\[([^\]]+)\]$/.exec(key);
        if (match && !hiddenNames.has(match[1])) entries.push({key, name: match[1], value});
      }
      return entries;
    }

    function renderSummary() {
      if (!summary) return;
      summary.replaceChildren();
      for (const entry of selectedFilters()) {
        if (entry.name === 'sortby' || entry.name === 'sortdir') continue;
        const field = filter.elements.namedItem(entry.key);
        const label = field?.getAttribute('aria-label') || field?.labels?.[0]?.textContent.trim();
        const value = entry.value ? field?.selectedOptions?.[0]?.textContent.trim() || entry.value : '(blank)';

        const row = document.createElement('li');
        row.textContent = (label || entry.name) + ': ' + value;
        summary.append(row);
      }
      if (!summary.childElementCount) {
        const row = document.createElement('li');
        row.textContent = 'This report has no filter fields.';
        summary.append(row);
      }
    }

    function setOpen(isOpen) {
      panel.hidden = !isOpen;
      if (autosubmit) filter.toggleAttribute('data-autosubmit', !isOpen);
      if (open) open.setAttribute('aria-expanded', String(isOpen));
      if (isOpen) renderSummary();
    }

    if (open) {
      open.disabled = false;
      // Delay the filter field's blur until the panel has suspended auto-apply.
      open.addEventListener('pointerdown', event => {
        if (panel.hidden && event.button === 0) event.preventDefault();
      });
      open.addEventListener('click', () => {
        setOpen(panel.hidden);
        if (!panel.hidden) {
          panel.scrollIntoView({block: 'start'});
          panel.querySelector('input:not([type="hidden"])')?.focus({preventScroll: true});
        }
      });
    }
    if (cancel) cancel.addEventListener('click', () => {
      setOpen(false);
      open?.focus();
    });
    $(filter).on('input change', renderSummary);
    panel.addEventListener('submit', () => {
      panel.querySelectorAll('[data-schedule-captured]').forEach(input => input.remove());
      for (const entry of selectedFilters()) {
        const input = document.createElement('input');
        input.type = 'hidden';
        input.name = entry.key;
        input.value = entry.value;
        input.dataset.scheduleCaptured = '1';
        panel.append(input);
      }
    });
    submit.disabled = false;
    setOpen(!panel.hidden);
  }

  window.fwCronEditor = {
    init: function (scope) {
      includeSelf(scope, '[data-cron-editor]').forEach(setupEditor);
      includeSelf(scope, '[data-report-schedule-scope]').forEach(setupReport);
    }
  };
})();
