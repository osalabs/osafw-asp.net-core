// Set theme to the user's preferred color scheme
function getPrefUIMode() {
  let mode = document.documentElement.getAttribute('data-bs-theme');
  if (mode) return mode;
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}
function setUIMode(mode) {
  if (mode === 'auto' && window.matchMedia('(prefers-color-scheme: dark)').matches) {
    document.documentElement.setAttribute('data-bs-theme', 'dark');
  } else {
    document.documentElement.setAttribute('data-bs-theme', mode);
  }
}
setUIMode(getPrefUIMode());
