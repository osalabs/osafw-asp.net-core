// override app defaults:
let fwApp = {
    setup() {
        const fwStore = useFwStore();
        onMounted(() => {
            // TODO
        });
        // watch(
        //     () => some_object_or_var,
        //     (newValue, oldValue) => {
        //         console.log('Var or nested property changed:', newValue);
        //     },
        //     { deep: true }
        // );

        return {
            fwStore // at least fwStore
        };
    }
};
<~/common/vue/app_simple.js>
