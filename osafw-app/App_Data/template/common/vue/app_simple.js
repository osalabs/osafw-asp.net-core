// minimal app for simple Vue screens
import { createApp } from 'vue';
import { createPinia } from 'pinia';

const mainApp = {
    async mounted() {
        const store = useSimpleStore();
        const ds = this.$el.parentElement.dataset;
        if (ds.base_url) store.base_url = ds.base_url;
        store.initApi();
        await store.loadData();
    }
};

const app = createApp(mainApp);
window.fwApp = app;
const pinia = createPinia();
app.use(pinia);

// expose store globally for templates
window.simpleStore = useSimpleStore();

// components can be added here if needed

// mounted in layout/vue/sys_footer.html
