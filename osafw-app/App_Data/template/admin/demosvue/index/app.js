//load store.js first with const useFwStore

const mainApp = {
  // data() {
  //   return {
  //     counter: 0,
  //     layout: 1
  //   }
  // },
  computed:{
  },
  mounted() {
    console.log('mainApp mounted');
    const fwStore = useFwStore();
    // assign all data from this.$el.parentElement.dataset to keys existing in fwStore
      Object.keys(this.$el.parentElement.dataset).forEach(key => {
          console.log("data key:", key,"=", this.$el.parentElement.dataset[key]);
          console.log("fwStore key:", fwStore[key]);
        if (fwStore[key] !== undefined) fwStore[key] = this.$el.parentElement.dataset[key];
    });
    //fwStore.base_url = this.$el.parentElement.dataset.baseUrl;
    //fwStore.related_id = this.$el.parentElement.dataset.relatedId;
    fwStore.loadIndex();
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

const app = createApp(mainApp);
window.fwApp = app; //make app available for components below in html

const pinia = createPinia();
app.use(pinia);

// https://github.com/vueform/multiselect
//app.component('multiselect', VueformMultiselect);

//components - add load to vue_components

//mounted to #fw-app in /layout/vue/sys_footer
