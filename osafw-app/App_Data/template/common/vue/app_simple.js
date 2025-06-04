// minimal app for simple Vue screens
// imports are done in /layout/vue/sys_footer.html

let mainApp = {
    setup() {
        const fwStore = useFwStore()
        return { fwStore } //everything returned here is reactive & template-visible
    },
    async mounted() {
        this.fwStore.saveToStore(this.$el.parentElement.dataset);
        this.fwStore.initApi();
        await this.fwStore.loadData();
    }
};

//merge in fwApp if defined
if (typeof fwApp !== 'undefined') {
    mainApp = AppUtils.deepMerge(mainApp, fwApp);
}

const app = createApp(mainApp);
app.use(createPinia());

window.fwApp = app; //make global

//components - add load to vue_components

//mounted to #fw-app in /layout/vue/sys_footer
