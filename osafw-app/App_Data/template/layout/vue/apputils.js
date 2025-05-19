const AppUtils = {
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
  }

};