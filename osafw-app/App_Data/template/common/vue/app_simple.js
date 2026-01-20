// minimal app for simple Vue screens
// imports are done in /layout/vue/sys_footer.html
<~/common/vue/app_core.js>

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

const app = createFwApp(mainApp);

//components - add load to vue_components

//mounted to #fw-app in /layout/vue/sys_footer
