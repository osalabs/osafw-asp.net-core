//load store.js first with const useFwStore

const mainApp = {
  //data: () => ({
  //    counter: 0
  //}),
  computed:{
  },
  mounted() {
    console.log('mainApp mounted');
    const fwStore = useFwStore();
    // assign all data from this.$el.parentElement.dataset to keys existing in fwStore
      Object.keys(this.$el.parentElement.dataset).forEach(key => {
          console.log("data key:", key,"=", this.$el.parentElement.dataset[key]);
        if (fwStore[key] !== undefined) fwStore[key] = this.$el.parentElement.dataset[key];
    });
  },
  updated() {
    //console.log('mainApp updated');
  },
  methods:{
    reload(){
      window.location.reload();
    },
  }
};

const app = createApp(mainApp);
window.fwApp = app; //make app available for components below in html

const pinia = createPinia();
app.use(pinia);

//components - add load to vue_components

//mounted to #fw-app in /layout/vue/sys_footer
