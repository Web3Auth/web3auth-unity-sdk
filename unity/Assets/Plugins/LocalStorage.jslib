mergeInto(LibraryManager.library, 
{

  Hello: function() {
    window.localStorage.setItem("token", "The tokken from Web3Auth goes here!");
  },

  GetLocalStorage: function(key) {
    var loadData = window.localStorage.getItem(UTF8ToString(key));
    
    var bufferSize = lengthBytesUTF8(loadData) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(loadData, buffer, bufferSize);
    return buffer;
  }

});
