const AppUtils = {
  failStd(jqXHR, textStatus, error) {
    //standard fail processing
    var err = textStatus + ", " + error;
    fw.error("Request Failed: " + err);
  }
};