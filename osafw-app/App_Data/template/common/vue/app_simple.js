// minimal app for simple Vue screens
// imports are done in /layout/vue/sys_footer.html

const mainApp = {
  async mounted() {
    const fwStore = useFwStore();
    fwStore.saveToStore(this.$el.parentElement.dataset);
    fwStore.initApi();
    await fwStore.loadData();
  }
};

const app = createApp(mainApp);
window.fwApp = app;

const pinia = createPinia();
app.use(pinia);

//components - add load to vue_components

//mounted to #fw-app in /layout/vue/sys_footer
