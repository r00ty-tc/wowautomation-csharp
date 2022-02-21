# wowautomation-csharp

There are 2 programs here one is called wowinstance and other is wowlauncher

# wowinstance

this is the main program that will open
- get data from arguments passed to the program, eg : username, password, realm, auctioneer name
- setup realm.wtf file to correct realm name
- setup correct realm so that we dont get a realm select screen
- open wow
- login user
- enter the character select screen
- enter the game type `/tar <auctioneer name>`
- interact with the selected npc using the keybind passed
- make sure the auction screen is open 
- start scan using `/auc scan`
- once scan is complete the wow.exe will close
- once the application is closed, 

the automation program will do a pixel check on all the pages to see that we are in the correct screen. Eg: login screen we test for a login button to check we are on the login screen

If the program finds out that we are in the wrong screen then we just terminate the program and we return a non 0 exit code to report an error.

This program needs a config file below are a sample of what is needed.

```[config]
base_url=https://www.web-auctioneer.com/
query_url=private-api/getrealmdata
wow_path_wotlk=H:\World Of Warcraft-335-clean\World.of.Warcraft.3.3.5a\
wow_path_tbc=E:\downloads\TBC-2.4.3.8606-Repack\TBC-2.4.3.8606-Repack\
```
# wowlauncher

this program will communicate with the server and finds out if any scans are pending

- poll server to check if any scans are pending
- if there are scans pending, get all the data eg: username, password, realm_id, faction, auctioneer name 
- pass all this data to wowinstance.exe 
- check if that wowinstance is still open, if it has closed then check the return code, non zero return code is error, if there is an error just do nothing and move on
- if the return code is 0 (success) read the (`WTF\\Account\\<account_name>\\SavedVariables\\Auc-ScanData.lua`) aucscan data and upload the file to the server. 

# How to set up wow (wotlk, tbc right now)
- the wow.exe should be always called `wow.exe`
- set the resolution to 800x600( i have tested on this only). we do pixel tests i have not tested it on other resolutions
- place your bot char near an auctioneer, and note the name, you should be able to highlight the auctioneer using the command `/tar <auctioneer_name>`
- bind a key for the action interact with character i set it to `]`. this key will be set in your realm data field.
- make sure you can interact with the auctioneer with this new key
- open up the auction house and make sure the window is on top left corner of the screen, as far as you can push it. If not the luancher pixel test will fail.
- in the auctioneer setting, go to the advanced setttings and look for `When scan is complete` section and select it to shutdown client.
- this is important as we will know when the scan is complete. when the application closes by itself

