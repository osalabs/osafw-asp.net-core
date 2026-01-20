// shared Vue app setup helpers
function createFwApp(appDefinition) {
    let mainApp = appDefinition;
    if (typeof fwApp !== 'undefined') {
        mainApp = AppUtils.deepMerge(mainApp, fwApp);
    }

    const app = createApp(mainApp);
    app.use(createPinia());
    app.config.globalProperties.AppUtils = AppUtils;

    window.fwApp = app; //make app available for components below in html
    return app;
}
