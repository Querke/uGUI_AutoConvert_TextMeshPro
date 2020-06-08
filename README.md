
# uGUI_AutoConvert_TextMeshPro
Helper Tool to automatically convert all of your Unity Text and InputFields to TextMeshPro equivalent components

Warning: This script might contain bugs, as it is a result of a community effort
Warning: You have to manually rename all your uGUI Text and InputFields in your script, and reference the new TextMeshPro components

This script was originally created by: BRUNO MIKOSKI (http://www.brunomikoski.com/playground/2015/3/31/convert-text-component-to-textmeshprougui-keeping-configurations)
The script was then modified by: SIMON TYSLAND (https://tinyurl.com/simtys)


## How to use

0. **BACKUP YOUR PROJECT FIRST!**
1. Go to Tools menu, and click on TextMeshPro AutoConverter
![1](https://i.imgur.com/07y7LVa.png)
2. Enter amount of text assets to be replaced
![2](https://i.imgur.com/guYsgIo.png)
3. Assign the normal Font asset that you want to replace, and assign TMP font asset that should replace the normal font asset.
4. Enter amount of loops, this means: How many text assets should be replaced in one go? If you have a very large UI with a lot of text asset, the converting process may take a some time. Therefor you can set the "loops" amount to something small, to see if you get the desired result. (One loop is never repeated twice).
5. Click "Execute!"
6. Go through all your scripts which are using uGUI text assets and replace them with TMP equivalents. (And reassign fields in editor where necessary)

