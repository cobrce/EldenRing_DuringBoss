### Introduction
A program for Elden Ring that detectes when the player starts or ends a fight against a boss then displays the fight duration.
Named after "During the boss battle" entry in DarkSouls 3 cheat engine table by the GrandArchive

### Credit : 
[ThaGrandArchive](https://github.com/The-Grand-Archives/Elden-Ring-CT-TGA) for the cheat table from which I extracted the pattern to find "GameDataMan" location

[BlackMagic](https://github.com/acidburn974/Blackmagic) (modified), a library to manipulate processes

### How to use:
By default the program will search for a process named "eldenring". (tested only with 1.12.3)
Start a boss fight, after the fight (win/loss/quit out/ memory of grace) the time elapsed is displayed in the console.

### Configuration

The first time you run the program it will create a "config.json" containing default configuration
``` json
{"Attach":true,
"ProcessName":"eldenring",
"Fullpath":"C:\\Program Files (x86)\\Steam\\steamapps\\common\\ELDEN RING\\Game\\eldenring.exe"}
```
Change the value of ProcessName if your game exe has a different name
You can also make the program run the game without EAC by changing the value of "Attach" to "False" and entering the full path of the game in the value of "FullPath"