$(document).on('change', '.on-change-table', function(e){
  var table_name = $(this).val();
  var iname = convertToCamelCasePlural(table_name);
  $('#model_name').val(iname);
});

$(document).on('change', '.on-change-model', function(e){
  var model_name = $(this).val();
  var url = '/Admin/'+model_name;
  var title = camelCaseToSingularTitle(model_name);
  $('#controller_url').val(url);
  $('#controller_title').val(title)
});


function convertToCamelCasePlural(input) {
  // Split the string by underscores to process snake_case input
  const words = input.split('_');

  // Map over the words, capitalize the first letter of each word except the first one
  const camelCaseWords = words.map((word, index) => {
    // Capitalize the first letter of the word if it's not the first word
    if (index > 0) {
      return word.charAt(0).toUpperCase() + word.slice(1).toLowerCase();
    }
    return word.charAt(0).toUpperCase() + word.slice(1);
  });

  // Join the words back together to form a single string
  let camelCaseOutput = camelCaseWords.join('');

  // Check if the last character is not 's', then append 's' for pluralization
  if (camelCaseOutput.slice(-1) !== 's') {
    camelCaseOutput += 's';
  }

  return camelCaseOutput;
}

function camelCaseToSingularTitle(input) {
  // Function to detect end of words by uppercase letters and split accordingly
  const words = input.match(/[A-Z0-9]+(?=[A-Z0-9][a-z0-9]|$)|[A-Z0-9][a-z0-9]*/g);

  if (!words) return input; // Return original if no matches (unlikely but safe)

  // Function to convert common plural words to singular
  const toSingular = (word) => {
    if (word.endsWith('ies')) {
      return word.replace(/ies$/, 'y');
    } else if (word.endsWith('s') && !word.endsWith('ss') && word.toLowerCase() !== 'gps') {
      return word.slice(0, -1);
    }
    return word;
  };

  // Apply the singular conversion to each word
  const singularWords = words.map(toSingular);

  // Join words with a space and capitalize the first letter of each word
  const title = singularWords.map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase()).join(' ');

  return title;
}
