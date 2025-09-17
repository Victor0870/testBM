Excel runtime integration package for BansheeGz BGDatabase asset (http://www.bansheegz.com/BGDatabase/), All Right Reserved (c)
Version: 1.1 (02/22/2020)
Release data: 07/20/2019

Features:
1) Update database data at runtime with data from Excel file [import]
2) Monitor excel file and auto-import the data if file changes
3) [v.1.1] Update Excel file at runtime with database data [export]

Setup:
1) Import BGDatabase package and create your own database as described here (http://www.bansheegz.com/BGDatabase/Setup/)
2) Move (not copy!) Assets\BansheeGz\BGDatabase\Editor\Libs\NPOI folder to Assets\Libs (or to any other folder, which is not under Editor folder)
3) Import BansheeGzExcelRuntime package
4) Add Assets\BansheeGzExcelRuntime\BGExcelImportGo.cs to your scene
5) Export required data to Excel as described here (http://www.bansheegz.com/BGDatabase/ThirdParty/Excel/)
6) Run your scene
7) Click to "Excel>>" button to access settings
8) Set "File" parameter to previously exported excel file location
9) Optionally set "monitoring" to true to auto monitor the file  
10) Optionally set "importOnStart" to run import on scene load
11) Optionally press "Save Settings" to save the settings
12) Open excel file, change the data and save the file (Ctrl+s). 
13) BGDatabase data should be updated. All binders in the scene will be executed after that. 
14) To export call BGExcelImportGo.Export method from your script 

Example:
Example scene is working with default database, shipped with BGDatabase package
This example shows, how to update Player.gold field with excel at runtime
Example files are located at Assets\BansheeGzExcelRuntime\Example\ folder

Example setup and run:
1) Create empty project
2) Import BGDatabase package
3) Move (not copy!) Assets\BansheeGz\BGDatabase\Editor\Libs\NPOI folder to Assets\Libs (or to any other folder, which is not under Editor folder) 
4) Import BansheeGzExcelRuntime package
5) Open Assets\BansheeGzExcelRuntime\Example\BGDatabaseExcelRuntimeExample.unity scene
6) Run the scene
7) Click on "Excel>>" button, and set "File" parameter to full Assets\BansheeGzExcelRuntime\Example\testData.xls file location (including drive, for example: c:\MyProject\Assets\BansheeGzExcelRuntime\Example\testData.xls)
8) Click on "Save Settings" button
9) Now open testData.xls file in Excel/OpenOffice/LibreOffice, change Player.gold value, and Save the file (Ctrl+s)     
10) Value in the database and text on the screen will be changed

Change log:
v.1.1 Export is added