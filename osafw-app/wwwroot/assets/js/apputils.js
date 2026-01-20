const AppUtils = {
    autocompleteSeparator: ' ::: ',
    failStd(jqXHR, textStatus, error) {
        //standard fail processing
        var err = textStatus + ", " + error;
        fw.error("Request Failed: " + err);
    },

    deepMerge(target, source) {
        if (typeof target !== 'object' || target === null) return source;
        if (typeof source !== 'object' || source === null) return target;

        const output = Array.isArray(target) ? [...target] : { ...target }; // Shallow clone

        for (const key in source) {
            if (source.hasOwnProperty(key)) {
                if (
                    typeof source[key] === 'object' &&
                    source[key] !== null &&
                    !(source[key] instanceof Function)
                ) {
                    output[key] = this.deepMerge(target[key] || {}, source[key]);
                } else {
                    output[key] = source[key];
                }
            }
        }

        return output;
    },

    htmlescape(str) {
        if (typeof str !== 'string') return str;
        return str.replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    },

    idFromAutocomplete(value) {
        if (typeof value !== 'string') return 0;
        const trimmedValue = value.trim();
        if (!trimmedValue) return 0;
        const separator = this.autocompleteSeparator.trim();
        const separatorIndex = trimmedValue.indexOf(separator);
        if (separatorIndex === -1) return 0;
        const idPart = trimmedValue.slice(separatorIndex + separator.length).trim();
        const id = parseInt(idPart, 10);
        return Number.isFinite(id) ? id : 0;
    },

    bytes2str: function (bytes) {
        var units = ['B', 'KiB', 'MiB', 'GiB', 'TiB'];
        var i = 0;
        while (bytes >= 1024 && i < units.length - 1) { bytes /= 1024; i++; }
        return Math.round(bytes * 10) / 10 + ' ' + units[i];
    },

    userLocale(global = {}) {
        // pick locale by user formats: MDY->en-US, DMY->en-GB
        const isDMY = (global.date_format ?? 0) == 10;
        return isDMY ? 'en-GB' : 'en-US';
    },

    is24h(global = {}) {
        return (global.time_format ?? 0) == 10;
    },

    dateFromServer(value) {
        if (!value) return null;
        if (value instanceof Date) return value;

        const t = String(value).trim();

        // date only
        const mDate = t.match(/^(\d{4})-(\d{2})-(\d{2})$/);
        if (mDate) {
            const [, y, m, d] = mDate;
            return new Date(Number(y), Number(m) - 1, Number(d));
        }

        // datetime (ignore timezone offsets as backend already sends user timezone)
        const mDateTime = t.match(/^(\d{4})-(\d{2})-(\d{2})[ T](\d{2}):(\d{2})(?::(\d{2})(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:?\d{2})?$/i);
        if (mDateTime) {
            const [, y, m, d, hh, mm, ss = '0'] = mDateTime;
            return new Date(Number(y), Number(m) - 1, Number(d), Number(hh), Number(mm), Number(ss));
        }

        const d = new Date(t);
        if (Number.isNaN(d.getTime())) return null;

        return d;
    },

    formatDate(value, global = {}) {
        const d = this.dateFromServer(value);
        if (!d) return value ?? '';
        return new Intl.DateTimeFormat(this.userLocale(global), {
            year: 'numeric',
            month: 'numeric',
            day: 'numeric',
        }).format(d);
    },

    formatDateTime(value, global = {}, withSeconds = true) {
        const d = this.dateFromServer(value);
        if (!d) return value ?? '';
        // build options per 12/24h, use value as-is without timezone conversion
        const opts = {
            year: 'numeric', month: 'numeric', day: 'numeric',
            hour: '2-digit', minute: '2-digit',
            hour12: !this.is24h(global),
        };
        if (withSeconds) opts.second = '2-digit';
        return new Intl.DateTimeFormat(this.userLocale(global), opts).format(d);
    },

    formatTime(value) {
        if (value === null || value === undefined || value === '') return '';
        const normalized = typeof value === 'number' ? value : Number.parseInt(value, 10);
        if (!Number.isFinite(normalized)) {
            const str = String(value).trim();
            return str.includes(':') ? str : '';
        }
        const safeSeconds = Math.max(0, normalized);
        const hours = Math.floor(safeSeconds / 3600);
        const minutes = Math.floor((safeSeconds - hours * 3600) / 60);
        return String(hours).padStart(2, '0') + ':' + String(minutes).padStart(2, '0');
    },

    timeToSeconds(value) {
        if (value === null || value === undefined || value === '') return 0;
        if (typeof value === 'number') return value;
        const str = String(value).trim();
        if (!str) return 0;
        if (/^\d+$/.test(str)) return Number.parseInt(str, 10);
        const parts = str.split(':');
        if (parts.length < 2) return 0;
        const hours = Number.parseInt(parts[0], 10);
        const minutes = Number.parseInt(parts[1], 10);
        if (!Number.isFinite(hours) || !Number.isFinite(minutes)) return 0;
        return Math.max(0, hours * 3600 + minutes * 60);
    },
};
