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
};
