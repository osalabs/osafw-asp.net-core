// minimal store for simple Vue screens
import { defineStore } from 'pinia';
import { mande } from 'mande';

let state = {
    base_url: '',
    api: null,
    data: null,
    is_loading: false,
    error: null,
};

let actions = {
    initApi() {
        this.api = mande(this.base_url);
    },
    async loadData() {
        this.is_loading = true;
        try {
            const res = await this.api.get('');
            this.data = res;
            this.error = null;
        } catch (err) {
            console.error('loadData error', err);
            this.error = err.body?.error || 'server error';
        } finally {
            this.is_loading = false;
        }
    }
};

export const useSimpleStore = defineStore('simple', {
    state: () => ({ ...state }),
    actions: actions,
});
window.useSimpleStore = useSimpleStore; // make global
