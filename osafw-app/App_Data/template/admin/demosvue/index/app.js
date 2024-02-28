//load store.js first

import { createApp } from 'vue';
import { createPinia } from 'pinia';

const mainApp = {
  store: mainStore,
  // data() {
  //   return {
  //     counter: 0,
  //     layout: 1
  //   }
  // },
  computed:{
  },
  mounted() {
    //console.log('mainApp mounted');
    //TODO this.$store.commit('setBaseUrl', this.$el.parentElement.dataset.baseUrl );
    //TODO this.$store.commit('setRelatedId', this.$el.parentElement.dataset.relatedId );
    //TODO this.$store.dispatch('loadAll');
  },
  updated() {
    //console.log('mainApp updated');
  },
  methods:{
    reload(){
      window.location.reload();
    }
  }
};

const pinia = createPinia();
const app = createApp(mainApp);

app.use(pinia);

// https://github.com/vueform/multiselect
//app.component('multiselect', VueformMultiselect);

//components - see separte vue templates

//mount
app.mount('#app');
console.log("app mounted");