let connection = new signalR.HubConnectionBuilder()
  .withUrl("/Hubs/PianoDisplay")
  .withAutomaticReconnect()
  .build();

//Key Aggregator
connection.on('KeyboardUpdate',
  function (state) {
    //keys 48-76
    state.keyChanges.forEach((keyData) => {
      var key = $(`#k${keyData.num}`);
      if (key) {
        if (keyData.on) {
          key.classList.add("On");
        } else {
          key.classList.remove("On");
        }
      }
    })
  });

//Individual key messages
connection.on('KeyDown',
  function (keyNum) {
    //keys 48-76
    var key = $(`#k${keyNum}`);
    if (key) {
      key.addClass("On");
    }
  });

connection.on('KeyUp',
  function (keyNum) {
    //keys 48-76
    var key = $(`#k${keyNum}`);
    if (key) {
      key.removeClass("On");
    }
  });

connection.start();