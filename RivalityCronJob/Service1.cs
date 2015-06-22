using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.IO;
// we need this to write to the file 
using System.Timers;
//we need this to create the timer.
using System.Data.Odbc;


namespace RivalityCronJob
{
    public partial class RivalityCronJob : ServiceBase
    {
        //Initialize the timer
        Timer timer = new Timer();

        //My sql connection object
        public ConnectToMySql ConnectToMySqlObj = ConnectToMySql.Instance;

        public RivalityCronJob()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            AddToFile(DateTime.Now + " Starting Service");

            //ad 1: handle Elapsed event
            timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);

            //ad 2: set interval to 1 minute (= 60,000 milliseconds)

            timer.Interval = 60000;

            //ad 3: enabling the timer
            timer.Enabled = true;

        }

        protected override void OnStop()
        {
            timer.Enabled = false;
            AddToFile(DateTime.Now + " Stopping Service");
            //Now disconnect the connection from MySQl Database
            ConnectToMySqlObj.myConn.Close();
        }

        private void AddToFile(string contents)
        {

            //set up a filestream
            FileStream fs = new
            FileStream(@"c:\cronjob.log", FileMode.OpenOrCreate, FileAccess.Write);

            //set up a streamwriter for adding text

            StreamWriter sw = new StreamWriter(fs);

            //find the end of the underlying filestream

            sw.BaseStream.Seek(0, SeekOrigin.End);

            //add the text
            sw.WriteLine(contents);
            //add the text to the underlying filestream

            try
            {
                //Rivality cron job
                //Read Configuration file from text file

                string cronJobConfigFilePath = @"C:\cronjob.config";
                string cronJobConfigline;

                //My sql connection object
                ConnectToMySql ConnectToMySqlObj = ConnectToMySql.Instance;

                if (File.Exists(cronJobConfigFilePath))
                {
                    StreamReader cronJobConfigfile = null;
                    try
                    {
                        cronJobConfigfile = new StreamReader(cronJobConfigFilePath);
                        while ((cronJobConfigline = cronJobConfigfile.ReadLine()) != null)
                        {
                            string filePath = cronJobConfigline;
                            string line;
                            string[] arrConfigurations = new string[1000];
                            string[] arrConfigurationValues = new string[1000];
                            string[] arrLineValue = new string[2];
                            string[] arrLineValueTemp = new string[2];
                            char[] spillterCharacter = new char[1];
                            char[] spillterCharacterTemp = new char[1];
                            string tempLineValue = "";
                            spillterCharacter[0] = '=';
                            spillterCharacterTemp[0] = '\'';
                            int counter = 0;
                            if (File.Exists(filePath))
                            {
                                StreamReader file = null;
                                try
                                {
                                    file = new StreamReader(filePath);
                                    while ((line = file.ReadLine()) != null)
                                    {
                                        if (line.Contains("="))
                                        {
                                            arrLineValue = line.Split(spillterCharacter);
                                            arrConfigurations[counter] = arrLineValue[0].Trim();
                                            tempLineValue = arrLineValue[1].Trim();
                                            if (tempLineValue.Contains("'"))
                                            {
                                                spillterCharacterTemp[0] = '\'';
                                                arrLineValueTemp = tempLineValue.Split(spillterCharacterTemp);
                                                tempLineValue = arrLineValueTemp[0].Trim();
                                            }
                                            if (tempLineValue.Contains("\""))
                                            {
                                                spillterCharacterTemp[0] = '"';
                                                arrLineValueTemp = tempLineValue.Split(spillterCharacterTemp);
                                                tempLineValue = arrLineValueTemp[1].Trim();
                                            }
                                            arrConfigurationValues[counter] = tempLineValue.Trim();
                                            counter = counter + 1;
                                        }
                                    }
                                }
                                finally
                                {
                                    if (file != null)
                                        file.Close();
                                }
                            }
                            else
                            {
                                Console.WriteLine("settings.inc.asp file not exist");
                                return;
                            }


                            //for text database connection

                            ConnectToMySqlObj.serverText = arrConfigurationValues[Array.IndexOf(arrConfigurations, "cTextDBIP")];
                            ConnectToMySqlObj.databaseText = arrConfigurationValues[Array.IndexOf(arrConfigurations, "cTextDBName")];
                            ConnectToMySqlObj.userText = arrConfigurationValues[Array.IndexOf(arrConfigurations, "cTextDBUser")];
                            ConnectToMySqlObj.passwordText = arrConfigurationValues[Array.IndexOf(arrConfigurations, "cTextDBPwd")];
                            ConnectToMySqlObj.ConnectForText();

                            string query = "SELECT txtID, txtText FROM tbltext WHERE txtLng = '" + arrConfigurationValues[Array.IndexOf(arrConfigurations, "cLanguage")] + "' ORDER BY txtID DESC";

                            OdbcCommand cmdForText = new OdbcCommand(query, ConnectToMySqlObj.myConnText);
                            OdbcDataReader ReadForText = cmdForText.ExecuteReader();

                            //text array
                            string[] text = new string[1000];
                            while (ReadForText.Read())
                            {
                                text[Convert.ToInt64(ReadForText["txtID"])] = Convert.ToString(ReadForText["txtText"]);
                            }

                            ReadForText.Close();
                            ConnectToMySqlObj.myConnText.Close();

                            QueueGo QueueGoObj = new QueueGo(ConnectToMySqlObj, arrConfigurations, arrConfigurationValues, text);
                            QueueGoObj.QueueGoProcess();
                            ConnectToMySqlObj.myConn.Close();
                        }
                    }
                    finally
                    {
                        if (cronJobConfigfile != null)
                            cronJobConfigfile.Close();
                    }
                }
                else
                {
                    Console.WriteLine("cronjob.config file not exist");
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);

                sw.WriteLine(trace.GetFrame(0).GetMethod().Name);
                sw.WriteLine("At line: " + trace.GetFrame(0).GetFileLineNumber());
                sw.WriteLine("Column: " + trace.GetFrame(0).GetFileColumnNumber());
                sw.WriteLine(ex.ToString());
                sw.WriteLine("Stacktrace: " + ex.StackTrace);
                sw.WriteLine(ex.Data);
            }

            sw.Flush();
            //close the writer
            sw.Close();



        }

        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            AddToFile(DateTime.Now + " Another entry");
        }

    }


    //Create My sql Connection class

    //Implement Multithreaded Singleton Pattern

    public sealed class ConnectToMySql
    {
        public OdbcConnection myConn;
        public OdbcConnection myConnText;
        public string server;
        public string database;
        public string user;
        public string password;
        public string serverText;
        public string databaseText;
        public string userText;
        public string passwordText;


        //Implement Singleton Pattern

        private static volatile ConnectToMySql instance;
        private static object objectlockCheck = new Object();

        //Constructor

        private ConnectToMySql()
        {


        }

        public void ConnectForText()
        {
            try
            {
                myConnText = new OdbcConnection("Driver={MySQL ODBC 5.1 Driver};Server=" + serverText + ";Database=" + databaseText + ";User=" + userText + ";Password=" + passwordText + ";");

                myConnText.Open();
            }
            catch (OdbcException MyOdbcException) //Catch any ODBC exception ..
            {
                for (int i = 0; i < MyOdbcException.Errors.Count; i++)
                {
                    Console.Write("ERROR #" + i + "\n" + "Message: " +
                    MyOdbcException.Errors[i].Message + "\n" +
                    "Native: " +
                    MyOdbcException.Errors[i].NativeError.ToString() + "\n" +
                    "Source: " +
                    MyOdbcException.Errors[i].Source + "\n" +
                    "SQL: " +
                    MyOdbcException.Errors[i].SQLState + "\n");
                }
            }
        }

        public void Connect()
        {
            try
            {
                myConn = new OdbcConnection("Driver={MySQL ODBC 5.1 Driver};Server=" + server + ";Database=" + database + ";User=" + user + ";Password=" + password + ";");
                //myConn = new OdbcConnection("Driver={MySQL ODBC 5.1 Driver};Server=localhost;Database=bandtycoon;User=root;Password=123456;");

                myConn.Open();
            }
            catch (OdbcException MyOdbcException) //Catch any ODBC exception ..
            {
                for (int i = 0; i < MyOdbcException.Errors.Count; i++)
                {
                    Console.Write("ERROR #" + i + "\n" + "Message: " +
                    MyOdbcException.Errors[i].Message + "\n" +
                    "Native: " +
                    MyOdbcException.Errors[i].NativeError.ToString() + "\n" +
                    "Source: " +
                    MyOdbcException.Errors[i].Source + "\n" +
                    "SQL: " +
                    MyOdbcException.Errors[i].SQLState + "\n");
                }
            }
        }

        //Retrun Singleton Object
        public static ConnectToMySql Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (objectlockCheck)
                    {
                        if (instance == null)
                            instance = new ConnectToMySql();
                    }
                }

                return instance;
            }
        }




        //Destructor
        ~ConnectToMySql()
        {
            try
            {
                if (myConn != null && myConn.State == ConnectionState.Open)
                {
                    //myConn.Close();
                }
            }
            catch (OdbcException MyOdbcException) //Catch any ODBC exception ..
            {
                for (int i = 0; i < MyOdbcException.Errors.Count; i++)
                {
                    Console.Write("ERROR #" + i + "\n" + "Message: " +
                    MyOdbcException.Errors[i].Message + "\n" +
                    "Native: " +
                    MyOdbcException.Errors[i].NativeError.ToString() + "\n" +
                    "Source: " +
                    MyOdbcException.Errors[i].Source + "\n" +
                    "SQL: " +
                    MyOdbcException.Errors[i].SQLState + "\n");
                }
            }

        }

    }



    public class QueueGo
    {
        public string cPageTitle, cRC4pwd;
        public long cStoreSpaceLvl0, cTowerBasDefBonus, cGeneralID, cAdminBuildingID;
        public float cLvl0Increase, cTotParamBonus, cSpeedConst;
        public decimal cFenceImg2Lvl, cMapSize, cMapWidth, cMapHeight, cInitBasType, cInitBasResources, cInitBasMoney, cInitBasXP, cBuildingCount, cBuildMinAdd, cCancelReturnMoney, cOilValue, cBarrelSize, cContainerSize;

        public decimal cListTroopsLineBr, cNewBaseReqEngineers, cNewBaseReqXP, cNewBaseReqMoney, cEngineerID, cMinMoney, cMaxMoney, cCostMoney;
        public decimal cMedalXP1, cNumMaxProjVip, cGuardTowerID, cXpScaler, cMoneyScaler, cRadarID, cNoRadarView, cManualPlanXP;
        public long cNumObjects, cSoldierID, cValveID, cFenceID, cFenceBasDefBonus, cSpyID;
        public decimal cMedalXP2, cDayMaxAttacks, cRanksPerPage, cNumMaxProj, cNewAllianceReqXP, cAllMaxMembers, cGreenDotH, cYellowDotH;
        public decimal cMedalWon1, cMedalWon2, cMedalWon3, cMedalXP3, cMedalSkill, cMedalTime, cRecruitPoints, cPoints, cAcademyID, cVipBuildTimeScale, cCancelMinutes;
        public decimal cCredSpeedInc;
        public double cE, cHourDiff, cCredProdInc;

        public string[] text;

        ConnectToMySql ConnectToMySqlObj;



        //Common Command for update, insert, delete
        OdbcCommand cmdRecSetCommon;

        //Constructor
        public QueueGo(ConnectToMySql ConnectToMySqlObjRef, string[] arrConfigurations, string[] arrConfigurationValues, string[] textValues)
        {

            ConnectToMySqlObjRef.server = arrConfigurationValues[Array.IndexOf(arrConfigurations, "cServerIP")];
            ConnectToMySqlObjRef.database = arrConfigurationValues[Array.IndexOf(arrConfigurations, "cDatabaseName")];
            ConnectToMySqlObjRef.user = arrConfigurationValues[Array.IndexOf(arrConfigurations, "cDatabaseUser")];
            ConnectToMySqlObjRef.password = arrConfigurationValues[Array.IndexOf(arrConfigurations, "cDatabasePwd")];
            ConnectToMySqlObjRef.Connect();

            cPageTitle = arrConfigurationValues[Array.IndexOf(arrConfigurations, "cPageTitle")]; //"Rivality.se - Dominera världen!";

            cHourDiff = Convert.ToDouble(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cHourDiff")]); //'Radius when viewing

            cMapSize = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cMapSize")]); //'Radius when viewing
            //'Map w & h: (A = w*h*4 + 2w + 2h + 1) w=h=5-> A = 5*5*4 + 4*5 +1 = 121 
            cMapWidth = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cMapWidth")]);// 'Radius of where to place a base - i.e. 300 * 300 * 4 = 360 000 places
            cMapHeight = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cMapHeight")]);// '-||-
            cInitBasType = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cInitBasType")]);// 'Base type (based on map location)
            cInitBasResources = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cInitBasResources")]);// 'Starting resources for a new base
            cInitBasMoney = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cInitBasMoney")]);// 'Starting money for a new base
            cInitBasXP = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cInitBasXP")]);// 'Starting XP for a new base
            cBuildingCount = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cBuildingCount")]);// 'Number of buildings in base -1
            cBuildMinAdd = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cBuildMinAdd")]);// 'Number of minutes to add when a user builds something already in queue
            cCancelReturnMoney = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cCancelReturnMoney")]);// '% of money to return when a project is cancelled
            cOilValue = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cOilValue")]);// 'Value of one oil barrel on the market
            cTowerBasDefBonus = Convert.ToInt64(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cTowerBasDefBonus")]);// '% bonus for one level of a guard tower
            cFenceBasDefBonus = Convert.ToInt64(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cFenceBasDefBonus")]);// '% bonus for one level of a fence
            cBarrelSize = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cBarrelSize")]);// 'Size of an oil barrel
            cContainerSize = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cContainerSize")]);// 'Size of a iron ore container
            cLvl0Increase = float.Parse(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cLvl0Increase")]);// 'Resources increase by this per hour without oil pump/iron mine etc.
            cE = Convert.ToDouble(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cE")]);// 'The constant e
            cListTroopsLineBr = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cListTroopsLineBr")]);// 'How many types of units should be listed per row at sendtroops.asp?
            cSpeedConst = float.Parse(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cSpeedConst")]);// 'Troop speed constant (i.e. how long time movement takes)
            cNewBaseReqEngineers = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cNewBaseReqEngineers")]);// 'How many engineers that are needed to establish a new base
            cNewBaseReqXP = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cNewBaseReqXP")]);// 'Required total user XP per each new base
            cNewBaseReqMoney = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cNewBaseReqMoney")]);// 'Cost to create a new base
            cEngineerID = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cEngineerID")]);// 'Engineer ID in database
            cSoldierID = Convert.ToInt64(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cSoldierID")]);// 'Soldier ID in database
            cMinMoney = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cMinMoney")]);// 'Minimum amount of money that could be sent/day
            cMaxMoney = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cMaxMoney")]);// 'Maximum amount of money that could be sent/day
            cCostMoney = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cCostMoney")]);// 'Cost to send money (percentage ofthe total amount)
            cNumObjects = Convert.ToInt64(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cNumObjects")]);// 'A value larger than the number of objects in db (for var dimensioning)
            cGuardTowerID = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cGuardTowerID")]);// 'Guard tower objID in DB
            cFenceID = Convert.ToInt64(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cFenceID")]);// 'Fence objID in DB
            cTotParamBonus = float.Parse(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cTotParamBonus")]);// 'Parameter bonus scalear
            cAdminBuildingID = Convert.ToInt64(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cAdminBuildingID")]);// 'Administration objID in DB
            cGeneralID = Convert.ToInt64(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cGeneralID")]);// 'General objID in DB
            cXpScaler = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cXpScaler")]);// 'XP gain from a battle scaler
            cMoneyScaler = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cMoneyScaler")]);// 'Money gain from an attack scaler
            cRadarID = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cRadarID")]);// 'Radar ID in DB
            cNoRadarView = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cNoRadarView")]);// 'Distance (in seconds) the user sees attacks if base has no radar
            cManualPlanXP = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cManualPlanXP")]);// 'XP needed to manually set plan parameters
            cRanksPerPage = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cRanksPerPage")]);// 'Number of ranks to display per page in rank.asp
            cNumMaxProj = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cNumMaxProj")]);// 'Max number of projects to run at the same time (i.e. build jobs)
            cNumMaxProjVip = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cNumMaxProjVip")]);// 'Max number of projects to run at the same time (i.e. build jobs) for VIP users
            cNewAllianceReqXP = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cNewAllianceReqXP")]);// 'Required XP to create an alliance
            cAllMaxMembers = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cAllMaxMembers")]);// 'Max members in a newly founded alliance
            cGreenDotH = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cGreenDotH")]);// 'How many hours since login that will repr. a green dot
            cYellowDotH = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cYellowDotH")]);// 'How many hours since login that will repr. a yellow dot
            cRC4pwd = arrConfigurationValues[Array.IndexOf(arrConfigurations, "cRC4pwd")];// 'RC4 Encryption password
            cStoreSpaceLvl0 = Convert.ToInt64(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cStoreSpaceLvl0")]);// 'Storage space at level 0
            cSpyID = Convert.ToInt64(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cSpyID")]);// 'Spy objID in db
            cDayMaxAttacks = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cDayMaxAttacks")]);// 'Maximum number of attacks on a user /day
            cVipBuildTimeScale = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cVipBuildTimeScale")]);// 'How much shorter in % build time is for VIP members
            cValveID = Convert.ToInt64(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cValveID")]);// 'Valve id in db
            cFenceImg2Lvl = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cFenceImg2Lvl")]);// 'Fence new design level
            cMedalXP1 = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cMedalXP1")]);// 'XP Level for bronze medal
            cMedalXP2 = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cMedalXP2")]);// 'XP Level for silver medal
            cMedalXP3 = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cMedalXP3")]);// 'XP Level for gold medal
            cMedalWon1 = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cMedalWon1")]);// 'Attacks won for bronze medal
            cMedalWon2 = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cMedalWon2")]);// 'Attacks won for silver medal
            cMedalWon3 = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cMedalWon3")]);// 'Attacks won for gold medal
            cMedalSkill = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cMedalSkill")]);// 'Skill ratio for medal (in percent)
            cMedalTime = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cMedalTime")]);// 'Days registred for medal
            cRecruitPoints = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cRecruitPoints")]);// 'Under this level you are a recruit
            cPoints = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cPoints")]);// 'Under this level you are a recruit
            cAcademyID = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cAcademyID")]);// 'Academy objID in db
            cCancelMinutes = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cCancelMinutes")]);// 'Cancel projects within this amount of time (neg value)

            //for text
            text = textValues;



            //'CREDITS
            cCredProdInc = Convert.ToDouble(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cCredProdInc")]); ; //'Increased production when using credits (in %)
            cCredSpeedInc = Convert.ToDecimal(arrConfigurationValues[Array.IndexOf(arrConfigurations, "cCredSpeedInc")]); ; //'Increased unit speed when using credits (in %)

            ConnectToMySqlObj = ConnectToMySqlObjRef;
            //Common Command for update, insert, delete
            cmdRecSetCommon = new OdbcCommand("", ConnectToMySqlObj.myConn);

        }



        //Process Queue go

        public void QueueGoProcess()
        {
            try
            {
                string query = "SELECT quID, quObjID, quUserID, quObjLoc, quObjBaseID, quObjCount, quType FROM tblqueue WHERE quFinished <= '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "' ORDER BY quFinished ASC";

                OdbcCommand cmdRecSet = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                OdbcDataReader RecSet = cmdRecSet.ExecuteReader();

                //Initialize variables
                long quType = 0;
                long spyMaxLevel = 0;
                string basXY = "";
                long basX = 0;
                long basY = 0;
                long basType = 0;

                if (RecSet.HasRows)
                {


                    while (RecSet.Read())
                    {

                        try
                        {
                            quType = Convert.ToInt64(RecSet["quType"].ToString());

                            //Unit movement or build object?
                            if (quType == 0) //'Build object
                            {

                                //'Getting info about if the user have the object since before
                                query = "SELECT (1) FROM tbluobj WHERE uObjID = " + RecSet["quObjID"].ToString().Trim() + " AND uObjuID = " + RecSet["quUserID"].ToString().Trim() + " AND uObjLoc = " + RecSet["quObjLoc"].ToString().Trim() + " AND uObjBaseID = " + RecSet["quObjBaseID"].ToString().Trim();

                                OdbcCommand cmdRecSet2 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                                OdbcDataReader RecSet2 = cmdRecSet2.ExecuteReader();

                                //'If Eof or no data then insert, otherwise update


                                if (!RecSet2.HasRows)
                                {
                                    //No Data
                                    if (Convert.ToInt64(RecSet["quObjID"].ToString().Trim()) == Convert.ToInt64(cSpyID.ToString().Trim()))
                                    {
                                        //'If spy, check tblspy for existing spy
                                        //'Get max level

                                        query = "SELECT objMaxLevel FROM tblobjects WHERE objID = " + cSpyID;

                                        OdbcCommand cmdRecSet3 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                                        OdbcDataReader RecSet3 = cmdRecSet3.ExecuteReader();

                                        spyMaxLevel = 0;
                                        if (RecSet3.HasRows) // equivalent to Not EOF
                                        {
                                            spyMaxLevel = Convert.ToInt64(RecSet3["objMaxLevel"].ToString());
                                        }

                                        //First close previous odbc reader
                                        RecSet3.Close();

                                        cmdRecSet3.CommandText = "SELECT spyLevel FROM tblspy WHERE spyUserID = " + RecSet["quUserID"].ToString().Trim() + " AND spyBaseID = " + RecSet["quObjBaseID"].ToString().Trim();
                                        RecSet3 = cmdRecSet3.ExecuteReader();

                                        if (RecSet3.HasRows) // equivalent to Not EOF
                                        {
                                            if (!(Convert.ToInt64(RecSet3["spyLevel"].ToString().Trim()) > spyMaxLevel))
                                            {
                                                //'Update spy level
                                                cmdRecSetCommon.CommandText = "UPDATE tblspy SET spyLevel = spyLevel + 1 WHERE spyUserID = " + RecSet["quUserID"].ToString().Trim() + " AND spyBaseID = " + RecSet["quObjBaseID"].ToString().Trim();
                                                cmdRecSetCommon.ExecuteNonQuery();
                                            }
                                        }
                                        else
                                        {
                                            //'Insert new spy
                                            cmdRecSetCommon.CommandText = "Insert Into tbluobj(uObjuID, uObjID, uObjCount, uObjLoc, uObjBaseID, uObjLevel) Values(" + RecSet["quUserID"].ToString().Trim() + ", " + RecSet["quObjID"].ToString().Trim() + ", " + RecSet["quObjCount"].ToString().Trim() + ", " + RecSet["quObjLoc"].ToString().Trim() + ", " + RecSet["quObjBaseID"].ToString().Trim() + ", 1)";
                                            cmdRecSetCommon.ExecuteNonQuery();
                                        }

                                        //Close all used command and reader in the scope
                                        RecSet3.Close();

                                    } // End of if (Convert.ToInt64(RecSet["quObjID"].ToString()) == Convert.ToInt64(cSpyID.ToString()))
                                    else
                                    {
                                        //'Adding post with the number of objects
                                        cmdRecSetCommon.CommandText = "Insert Into tbluobj(uObjuID, uObjID, uObjCount, uObjLoc, uObjBaseID, uObjLevel) Values(" + RecSet["quUserID"].ToString().Trim() + ", " + RecSet["quObjID"].ToString().Trim() + ", " + RecSet["quObjCount"].ToString().Trim() + ", " + RecSet["quObjLoc"].ToString().Trim() + ", " + RecSet["quObjBaseID"].ToString().Trim() + ", 1)";
                                        cmdRecSetCommon.ExecuteNonQuery();

                                    } // End of else (Convert.ToInt64(RecSet["quObjID"].ToString()) == Convert.ToInt64(cSpyID.ToString()))

                                    cmdRecSetCommon.CommandText = "UPDATE tblbase SET basXP = basXP + (SELECT objXP FROM tblobjects WHERE objID = " + RecSet["quObjID"].ToString().Trim() + ") * " + RecSet["quObjCount"].ToString().Trim() + " WHERE basID = " + RecSet["quObjBaseID"].ToString().Trim();
                                    cmdRecSetCommon.ExecuteNonQuery();

                                } // End of if (!RecSet2.HasRows)
                                else
                                {
                                    //Data Exists
                                    //'Updating the post with the number of objects or level
                                    if (Convert.ToInt64(RecSet["quObjID"].ToString().Trim()) == Convert.ToInt64(cSpyID.ToString().Trim()))
                                    {
                                        //'Tactical recon team
                                        cmdRecSetCommon.CommandText = "UPDATE tbluobj Set uObjLevel = uObjLevel + 1 WHERE " + RecSet["quUserID"].ToString().Trim() + " AND uObjID = " + RecSet["quObjID"].ToString().Trim() + " AND uObjBaseID = " + RecSet["quObjBaseID"].ToString().Trim();
                                        cmdRecSetCommon.ExecuteNonQuery();

                                        cmdRecSetCommon.CommandText = "UPDATE tblbase SET basXP = basXP + (SELECT objXP FROM tblobjects WHERE objID = " + RecSet["quObjID"].ToString().Trim() + ") WHERE basID = " + RecSet["quObjBaseID"].ToString().Trim();
                                        cmdRecSetCommon.ExecuteNonQuery();
                                    } // End of if(Convert.ToInt64(RecSet["quObjID"].ToString()) == Convert.ToInt64(cSpyID.ToString()))
                                    else
                                    {
                                        if (RecSet["quObjLoc"].ToString().Trim() == "99") //'Units - add to count
                                        {
                                            cmdRecSetCommon.CommandText = "UPDATE tbluobj Set uObjCount = uObjCount + " + RecSet["quObjCount"].ToString().Trim() + " WHERE uObjuID = " + RecSet["quUserID"].ToString().Trim() + " AND uObjID = " + RecSet["quObjID"].ToString().Trim() + " AND uObjLoc = " + RecSet["quObjLoc"].ToString().Trim() + " AND uObjBaseID = " + RecSet["quObjBaseID"].ToString().Trim();
                                            cmdRecSetCommon.ExecuteNonQuery();
                                            cmdRecSetCommon.CommandText = "UPDATE tblbase SET basXP = basXP + (SELECT objXP FROM tblobjects WHERE objID = " + RecSet["quObjID"].ToString().Trim() + ") * " + RecSet["quObjCount"].ToString().Trim() + " WHERE basID = " + RecSet["quObjBaseID"].ToString().Trim();
                                            cmdRecSetCommon.ExecuteNonQuery();
                                        } // End of if (RecSet["quObjLoc"].ToString() == "99")
                                        else //'Building - add to level
                                        {
                                            cmdRecSetCommon.CommandText = "UPDATE tbluobj Set uObjLevel = uObjLevel + 1 WHERE uObjuID = " + RecSet["quUserID"].ToString().Trim() + " AND uObjID = " + RecSet["quObjID"].ToString().Trim() + " AND uObjLoc = " + RecSet["quObjLoc"].ToString().Trim() + " AND uObjBaseID = " + RecSet["quObjBaseID"].ToString().Trim();
                                            cmdRecSetCommon.ExecuteNonQuery();
                                            cmdRecSetCommon.CommandText = "UPDATE tblbase SET basXP = basXP + (SELECT objXP FROM tblobjects WHERE objID = " + RecSet["quObjID"].ToString().Trim() + ") WHERE basID = " + RecSet["quObjBaseID"].ToString().Trim();
                                            cmdRecSetCommon.ExecuteNonQuery();
                                        } // End of else (RecSet["quObjLoc"].ToString() == "99")

                                    } // end of else(Convert.ToInt64(RecSet["quObjID"].ToString()) == Convert.ToInt64(cSpyID.ToString()))


                                } // End of else (!RecSet2.HasRows)

                                //Close all used command and reader in the scope
                                RecSet2.Close();

                            } // End of if (quType == 0)
                            else if (quType == 1) //'Unit movement
                            {


                                //'Getting info about if the user have the object since before
                                query = "SELECT (1) FROM tbluobj WHERE uObjID = " + RecSet["quObjID"].ToString().Trim() + " AND uObjuID = " + RecSet["quUserID"].ToString().Trim() + " AND uObjBaseID = " + RecSet["quObjBaseID"].ToString().Trim();
                                OdbcCommand cmdRecSet2 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                                OdbcDataReader RecSet2 = cmdRecSet2.ExecuteReader();

                                //'If Eof then insert, otherwise update

                                if (!RecSet2.HasRows)
                                {
                                    //'Adding post with the number of objects
                                    cmdRecSetCommon.CommandText = "Insert Into tbluobj(uObjuID, uObjID, uObjCount, uObjLoc, uObjBaseID, uObjLevel) Values(" + RecSet["quUserID"].ToString().Trim() + ", " + RecSet["quObjID"].ToString().Trim() + ", " + RecSet["quObjCount"].ToString().Trim() + ", 99, " + RecSet["quObjBaseID"].ToString().Trim() + ", 1)";
                                    cmdRecSetCommon.ExecuteNonQuery();

                                } //End of if (!RecSet2.HasRows)
                                else
                                {
                                    //'Updating the post with the number of units
                                    cmdRecSetCommon.CommandText = "UPDATE tbluobj Set uObjCount = uObjCount + " + RecSet["quObjCount"].ToString().Trim() + " WHERE uObjuID = " + RecSet["quUserID"].ToString().Trim() + " AND uObjID = " + RecSet["quObjID"].ToString().Trim() + " AND uObjLoc = 99 AND uObjBaseID = " + RecSet["quObjBaseID"].ToString().Trim();
                                    cmdRecSetCommon.ExecuteNonQuery();

                                }//End of else (!RecSet2.HasRows)

                                //'Adding to economy table 
                                try
                                {
                                    fTransLog(fBaseToUser(Convert.ToInt64(RecSet["quObjBaseID"])), Convert.ToInt64(RecSet["quObjBaseID"]), -9, 0);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);

                                    //set up a filestream
                                    FileStream fs = new
                                    FileStream(@"c:\cronjob.log", FileMode.OpenOrCreate, FileAccess.Write);
                                    //set up a streamwriter for adding text
                                    StreamWriter sw = new StreamWriter(fs);
                                    //find the end of the underlying filestream
                                    sw.BaseStream.Seek(0, SeekOrigin.End);
                                    sw.WriteLine("Error at fTransLog, quType = 1");
                                    sw.WriteLine(trace.GetFrame(0).GetMethod().Name);
                                    sw.WriteLine("Line: " + trace.GetFrame(0).GetFileLineNumber());
                                    sw.WriteLine("Column: " + trace.GetFrame(0).GetFileColumnNumber());
                                    sw.WriteLine(ex.ToString());
                                    sw.Flush();
                                    sw.Close();
                                }

                            }//End of else if (quType == 1)
                            else if (quType == 2) //'Create base
                            {
                                //'Base x,y and type
                                basXY = RecSet["quObjBaseID"].ToString();
                                basX = Convert.ToInt64(getXY(basXY, "x").ToString().Trim());
                                basY = Convert.ToInt64(getXY(basXY, "y").ToString().Trim());
                                //'basType = baseType(basX,basY)
                                basType = 1;

                                query = "INSERT INTO tblbase (basUserID, basName, basType, basResources, basMoney, basX, basY, basXP, basHQ, basLastUpd) VALUES(" + RecSet["quUserID"].ToString().Trim() + ", 'New base', " + basType + ", " + cInitBasResources + ", " + cInitBasMoney + ", " + basX + ", " + basY + ", " + cInitBasXP + ",0,'" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "')";
                                cmdRecSetCommon.CommandText = query;
                                cmdRecSetCommon.ExecuteNonQuery();
                            } // End of else if (quType == 2)
                            else if (quType == 3) //'Attack
                            {

                            }//End of else if (quType == 3)

                            //'Deleting post
                            cmdRecSetCommon.CommandText = "DELETE FROM tblqueue WHERE quID = " + RecSet["quID"].ToString().Trim();
                            cmdRecSetCommon.ExecuteNonQuery();

                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);

                            //set up a filestream
                            FileStream fs = new
                            FileStream(@"c:\cronjob.log", FileMode.OpenOrCreate, FileAccess.Write);
                            //set up a streamwriter for adding text
                            StreamWriter sw = new StreamWriter(fs);
                            //find the end of the underlying filestream
                            sw.BaseStream.Seek(0, SeekOrigin.End);
                            sw.WriteLine(trace.GetFrame(0).GetMethod().Name);
                            sw.WriteLine("Line: " + trace.GetFrame(0).GetFileLineNumber());
                            sw.WriteLine("Column: " + trace.GetFrame(0).GetFileColumnNumber());
                            sw.WriteLine(ex.ToString());
                            sw.Flush();
                            sw.Close();
                        }


                    } // End of while (RecSet.Read())

                } //End of if (RecSet.HasRows)


                //'Attacks
                query = "SELECT * FROM tblattack WHERE attArrTime <= '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "' AND attReturning = 0 GROUP BY attGroupID ORDER BY attArrTime ASC";
                OdbcCommand cmdRecSet4 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                OdbcDataReader RecSet4 = cmdRecSet4.ExecuteReader();

                string groupID = "";
                long attReturning = -1;

                while (RecSet4.Read())
                {
                    groupID = RecSet4["attGroupID"].ToString();
                    attReturning = Convert.ToInt64(RecSet4["attReturning"].ToString().Trim());
                    if (attReturning == 0)
                    {
                        //'Adding to economy table 
                        try
                        {
                            attack(groupID, 0);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);

                            //set up a filestream
                            FileStream fs = new
                            FileStream(@"c:\cronjob.log", FileMode.OpenOrCreate, FileAccess.Write);
                            //set up a streamwriter for adding text
                            StreamWriter sw = new StreamWriter(fs);
                            //find the end of the underlying filestream
                            sw.BaseStream.Seek(0, SeekOrigin.End);
                            sw.WriteLine("Error at attack, groupID = " + groupID);
                            sw.WriteLine(trace.GetFrame(0).GetMethod().Name);
                            sw.WriteLine("Line: " + trace.GetFrame(0).GetFileLineNumber());
                            sw.WriteLine("Column: " + trace.GetFrame(0).GetFileColumnNumber());
                            sw.WriteLine(ex.ToString());
                            sw.Flush();
                            sw.Close();
                        }

                    }
                }// End of while (RecSet4.Read())

                //'Returning attacks

                //First close previous odbc reader
                RecSet4.Close();
                query = "SELECT * FROM tblattack WHERE attArrTime <= '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "' AND attReturning = 1";
                cmdRecSet4.CommandText = query;
                RecSet4 = cmdRecSet4.ExecuteReader();

                long attObjID = 0;
                long attObjCount = 0;
                long attUserID = 0;
                long attBaseID = 0;
                long attID = 0;

                while (RecSet4.Read())
                {
                    attObjID = Convert.ToInt64(RecSet4["attObjID"].ToString().Trim());
                    attObjCount = Convert.ToInt64(RecSet4["attObjCount"].ToString().Trim());
                    attUserID = Convert.ToInt64(RecSet4["attUserID"].ToString().Trim());
                    attBaseID = Convert.ToInt64(RecSet4["attBaseID"].ToString().Trim());
                    attID = Convert.ToInt64(RecSet4["attID"].ToString().Trim());

                    //'Getting info about if the user have the object since before
                    query = "SELECT (1) FROM tbluobj WHERE uObjID = " + attObjID + " AND uObjuID = " + attUserID + " AND uObjBaseID = " + attBaseID;
                    OdbcCommand cmdRecSet2 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                    OdbcDataReader RecSet2 = cmdRecSet2.ExecuteReader();

                    //'If Eof then insert, otherwise update
                    if (!RecSet2.HasRows)
                    {
                        //'Adding post with the number of objects
                        cmdRecSetCommon.CommandText = "Insert Into tbluobj(uObjuID, uObjID, uObjCount, uObjLoc, uObjBaseID, uObjLevel) Values(" + attUserID + ", " + attObjID + ", " + attObjCount + ", 99, " + attBaseID + ", 1)";
                        cmdRecSetCommon.ExecuteNonQuery();
                    } //End of if(!RecSet2.HasRows)
                    else
                    {
                        //'Updating the post with the number of units
                        cmdRecSetCommon.CommandText = "UPDATE tbluobj Set uObjCount = uObjCount + " + attObjCount + " WHERE uObjuID = " + attUserID + " AND uObjID = " + attObjID + " AND uObjLoc = 99 AND uObjBaseID = " + attBaseID;
                        cmdRecSetCommon.ExecuteNonQuery();

                    } // End of else(!RecSet2.HasRows)

                    cmdRecSetCommon.CommandText = "DELETE FROM tblattack WHERE attID = " + attID;
                    cmdRecSetCommon.ExecuteNonQuery();

                }// End of while (RecSet4.Read())


                //'Spy (tactical recon team)

                //First close previous odbc reader
                RecSet4.Close();
                query = "SELECT * FROM tblspy WHERE spyArrTime <= '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "' AND spyReturning = 0";
                cmdRecSet4.CommandText = query;
                RecSet4 = cmdRecSet4.ExecuteReader();

                long spyID = 0;

                while (RecSet4.Read())
                {
                    spyID = Convert.ToInt64(RecSet4["spyID"].ToString().Trim());
                    try
                    {
                        spy(spyID);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);

                        //set up a filestream
                        FileStream fs = new
                        FileStream(@"c:\cronjob.log", FileMode.OpenOrCreate, FileAccess.Write);
                        //set up a streamwriter for adding text
                        StreamWriter sw = new StreamWriter(fs);
                        //find the end of the underlying filestream
                        sw.BaseStream.Seek(0, SeekOrigin.End);
                        sw.WriteLine(trace.GetFrame(0).GetMethod().Name);
                        sw.WriteLine("Line: " + trace.GetFrame(0).GetFileLineNumber());
                        sw.WriteLine("Column: " + trace.GetFrame(0).GetFileColumnNumber());
                        sw.WriteLine(ex.ToString());
                        sw.Flush();
                        sw.Close();

                    }
                }

                //'Returning tactical teams

                //First close previous odbc reader
                RecSet4.Close();
                query = "SELECT * FROM tblspy WHERE spyArrTime <= '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "' AND spyReturning = 1";
                cmdRecSet4.CommandText = query;
                RecSet4 = cmdRecSet4.ExecuteReader();

                long spyLevel = 0;
                long spyUserID = 0;
                long spyBaseID = 0;

                while (RecSet4.Read())
                {
                    spyID = Convert.ToInt64(RecSet4["spyID"].ToString().Trim());
                    spyLevel = Convert.ToInt64(RecSet4["spyLevel"].ToString().Trim());
                    spyUserID = Convert.ToInt64(RecSet4["spyUserID"].ToString().Trim());
                    spyBaseID = Convert.ToInt64(RecSet4["spyBaseID"].ToString().Trim());

                    //'Getting info about if the user have the object since before
                    query = "SELECT (1) FROM tbluobj WHERE uObjID = " + cSpyID + " AND uObjuID = " + spyUserID + " AND uObjBaseID = " + spyBaseID;
                    OdbcCommand cmdRecSet2 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                    OdbcDataReader RecSet2 = cmdRecSet2.ExecuteReader();
                    //'If Eof then insert, otherwise do nothing - only one spy per base!
                    if (!RecSet2.HasRows)
                    {
                        //'Adding post with the number of objects
                        cmdRecSetCommon.CommandText = "Insert Into tbluobj(uObjuID, uObjID, uObjCount, uObjLoc, uObjBaseID, uObjLevel) Values(" + spyUserID + ", " + cSpyID + ", " + 1 + ", 99, " + spyBaseID + ", " + spyLevel + ")";
                        cmdRecSetCommon.ExecuteNonQuery();
                    }
                    cmdRecSetCommon.CommandText = "DELETE FROM tblspy WHERE spyID = " + spyID;
                    cmdRecSetCommon.ExecuteNonQuery();
                }// End of while (RecSet4.Read())


                //'Missiles
                //First close previous odbc reader
                RecSet4.Close();
                query = "SELECT maID FROM tblmissileattack WHERE maArrTime <= '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "'";
                cmdRecSet4.CommandText = query;
                RecSet4 = cmdRecSet4.ExecuteReader();

                long maID = 0;

                while (RecSet4.Read())
                {
                    maID = Convert.ToInt64(RecSet4["maID"].ToString().Trim());
                    try
                    {
                        missile_attack(maID, 0);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);

                        //set up a filestream
                        FileStream fs = new
                        FileStream(@"c:\cronjob.log", FileMode.OpenOrCreate, FileAccess.Write);
                        //set up a streamwriter for adding text
                        StreamWriter sw = new StreamWriter(fs);
                        //find the end of the underlying filestream
                        sw.BaseStream.Seek(0, SeekOrigin.End);
                        sw.WriteLine(trace.GetFrame(0).GetMethod().Name);
                        sw.WriteLine("Line: " + trace.GetFrame(0).GetFileLineNumber());
                        sw.WriteLine("Column: " + trace.GetFrame(0).GetFileColumnNumber());
                        sw.WriteLine(ex.ToString());
                        sw.Flush();
                        sw.Close();
                    }
                }

                /* Excluded from file in second phase

                //'QUICK FIXES FOR KNOWN BUGS
                //'------------------------------------
                //'IMPORTANT: CHECK FOR CHANGES IN tblobjects - id's, levels etc. might differ'
                //'****************************************************************************''
                //'Delete negative values
                cmdRecSetCommon.CommandText = "DELETE FROM tbluobj WHERE uobjCount < 0";
                cmdRecSetCommon.ExecuteNonQuery();

                //'Set correct user for buildings
                cmdRecSetCommon.CommandText = "UPDATE IGNORE tbluobj AS uobj " + "INNER JOIN tblbase AS bas ON uobj.uobjbaseID = bas.basID " + "SET uobj.uobjuid = bas.basUserID " + "WHERE uobj.uobjloc <> 99 AND uobj.uobjuid <> bas.basUserID";
                cmdRecSetCommon.ExecuteNonQuery();

                //'No buildings higher than maxlevel
                cmdRecSetCommon.CommandText = "UPDATE tbluobj AS uobj " + "INNER JOIN tblobjects AS obj ON uobj.uobjID = obj.objID " + "SET uobj.uobjLevel = obj.objMaxLevel " + "WHERE uobj.uobjloc <> 99 AND uobj.uobjLevel > obj.objMaxLevel";
                cmdRecSetCommon.ExecuteNonQuery();

                //'Remove alliance id from users with alliance that doesn't exist	
                cmdRecSetCommon.CommandText = "UPDATE tbluser SET uAllID = 0 WHERE uallid > 0 And uallID NOT IN(SELECT allid FROM tblalliance)";
                cmdRecSetCommon.ExecuteNonQuery();

                //'Remove double bases
                cmdRecSetCommon.CommandText = "delete from tblbase where basid in (select basID FROM (select basID,count(*) as n from tblbase group by basX,basY having  n > 1) AS tbl)";
                cmdRecSetCommon.ExecuteNonQuery();

                //'Delete old login logs
                cmdRecSetCommon.CommandText = "DELETE FROM tbllogins WHERE loginTime < '" + DateTime.Now.AddDays(-5).ToString("yyyy-MM-dd dd:mm:ss") + "'";
                cmdRecSetCommon.ExecuteNonQuery();

                 */

                //Now close all data reader
                RecSet.Close();
                RecSet4.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);

                //set up a filestream
                FileStream fs = new
                FileStream(@"c:\cronjob.log", FileMode.OpenOrCreate, FileAccess.Write);
                //set up a streamwriter for adding text
                StreamWriter sw = new StreamWriter(fs);
                //find the end of the underlying filestream
                sw.BaseStream.Seek(0, SeekOrigin.End);
                sw.WriteLine(trace.GetFrame(0).GetMethod().Name);
                sw.WriteLine("Line: " + trace.GetFrame(0).GetFileLineNumber());
                sw.WriteLine("Column: " + trace.GetFrame(0).GetFileColumnNumber());
                sw.WriteLine(ex.ToString());
                sw.Flush();
                sw.Close();
            }
        }


        //End of Process Queue go

        //Process Attack

        public void attack(string groupID, decimal sim)
        {

            //'vars (outprint means out),(reference means ref),(reference2 means ref2)
            //var i, reference, reference2;
            long i, reference = 0;
            decimal reference2 = 0;
            string outprint = "";
            //'fixed arrays (RecSetAtt no need its for recoedset)
            long[,] off_army = new long[12 + 1, 2 + 1];
            long[,] off_unit = new long[12 + 1, 2 + 1];
            long[,] def_army = new long[12 + 1, 2 + 1];
            long[,] def_unit = new long[12 + 1, 2 + 1];
            long[,] units_type = new long[12 + 1, 2 + 1];
            long[,] losses_type = new long[12 + 1, 2 + 1];
            float[] bonus = new float[2 + 1];
            long[] xp = new long[2 + 1];
            //'Dim dynamic arrays
            //Dim idsInCat(), off(), def(), arrObjType(), arrObjName(), arrObjIcon(), units(), losses(), arrObjLevel(), unit_xp()
            //'ReDim dynamic arrays with values
            long[] idsInCat = new long[1 + 1];
            long[,] off = new long[cNumObjects + 1, 12 + 1];
            long[,] def = new long[cNumObjects + 1, 12 + 1];
            long[] arrObjType = new long[cNumObjects + 1];
            string[] arrObjName = new string[cNumObjects + 1];
            string[] arrObjIcon = new string[cNumObjects + 1];
            long[,] units = new long[cNumObjects + 1, 2 + 1];
            long[,] losses = new long[cNumObjects + 1, 2 + 1];
            long[,] arrObjLevel = new long[cNumObjects + 1, 2 + 1];
            long[] unit_xp = new long[cNumObjects + 1];

            //'Don't cancel
            bool cancel = false;

            //'Number of units
            long numUnits = fNumObjects("False");

            string out_all = "";
            string query_conquerbase = "";
            decimal code = 0;
            string query_def1 = "";
            string query_def2 = "";
            string query_att1 = "";
            string query_xp = "";
            string query_attrep = "";
            string query_defrep = "";
            string query_rep = "";
            //'Get Off/Def values
            string query = "SELECT * FROM tblobjects";
            OdbcCommand cmdRecSetAtt = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader RecSetAtt = cmdRecSetAtt.ExecuteReader();
            while (RecSetAtt.Read())
            {
                off[Convert.ToInt64(RecSetAtt["objID"]), 12] = Convert.ToInt64(RecSetAtt["objOff12"]);
                off[Convert.ToInt64(RecSetAtt["objID"]), 3] = Convert.ToInt64(RecSetAtt["objOff3"]);
                off[Convert.ToInt64(RecSetAtt["objID"]), 4] = Convert.ToInt64(RecSetAtt["objOff4"]);
                off[Convert.ToInt64(RecSetAtt["objID"]), 5] = Convert.ToInt64(RecSetAtt["objOff5"]);
                def[Convert.ToInt64(RecSetAtt["objID"]), 12] = Convert.ToInt64(RecSetAtt["objDef12"]);
                def[Convert.ToInt64(RecSetAtt["objID"]), 3] = Convert.ToInt64(RecSetAtt["objDef3"]);
                def[Convert.ToInt64(RecSetAtt["objID"]), 4] = Convert.ToInt64(RecSetAtt["objDef4"]);
                def[Convert.ToInt64(RecSetAtt["objID"]), 5] = Convert.ToInt64(RecSetAtt["objDef5"]);
                arrObjType[Convert.ToInt64(RecSetAtt["objID"])] = Convert.ToInt64(RecSetAtt["objCat"]);
                arrObjName[Convert.ToInt64(RecSetAtt["objID"])] = Convert.ToString(RecSetAtt["objName"]);
                arrObjIcon[Convert.ToInt64(RecSetAtt["objID"])] = Convert.ToString(RecSetAtt["objIcon"]);
                unit_xp[Convert.ToInt64(RecSetAtt["objID"])] = Convert.ToInt64(RecSetAtt["objXP"]);

            }

            //'Attacker
            query = "SELECT attObjID, attObjCount, attDist, attAggr, attMob, attBaseID, attTargetBaseID, attArrTime, attDepTime, attUserID, attReturning, attType FROM tblattack WHERE attGroupID = '" + groupID + "'";
            OdbcCommand cmdRecSetAtt2 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader RecSetAtt2 = cmdRecSetAtt2.ExecuteReader();

            long attBaseID = 0;
            long attUserID = 0;
            long attTargetBaseID = 0;
            DateTime attDepTime = DateTime.Now;
            DateTime attArrTime = DateTime.Now;
            long attReturning = 0;
            double attDist = 0;
            double attAggr = 0;
            double attMob = 0;
            long attType = 0;

            if (RecSetAtt2.HasRows)
            {
                attBaseID = Convert.ToInt64(RecSetAtt2["attBaseID"]);
                attUserID = Convert.ToInt64(RecSetAtt2["attUserID"]);
                attTargetBaseID = Convert.ToInt64(RecSetAtt2["attTargetBaseID"]);
                attDepTime = Convert.ToDateTime(RecSetAtt2["attDepTime"]);
                attArrTime = Convert.ToDateTime(RecSetAtt2["attArrTime"]);
                attReturning = Convert.ToInt64(RecSetAtt2["attReturning"]);
                attDist = Convert.ToDouble(RecSetAtt2["attDist"]);
                attAggr = Convert.ToDouble(RecSetAtt2["attAggr"]);
                attMob = Convert.ToDouble(RecSetAtt2["attMob"]);
                attType = Convert.ToInt64(RecSetAtt2["attType"]);
            }

            long attObjID;
            long attObjCount;
            long attObjType;

            while (RecSetAtt2.Read())
            {

                attObjID = Convert.ToInt64(RecSetAtt2["attObjID"]);
                attObjCount = Convert.ToInt64(RecSetAtt2["attObjCount"]);
                attObjType = Convert.ToInt64(arrObjType[attObjID]);
                if (attObjType == 1 || attObjType == 2)
                {
                    attObjType = 12;
                }
                units[attObjID, 1] = attObjCount;
                units_type[attObjType, 1] = units_type[attObjType, 1] + attObjCount;
                xp[2] = xp[2] + Convert.ToInt64(unit_xp[attObjID] * attObjCount);

                for (i = 3; i <= 6; i++)
                {
                    if (i == 6)
                    {
                        i = 12;
                    }
                    off_army[i, 1] = off_army[i, 1] + attObjCount * off[attObjID, i];
                    def_army[i, 1] = def_army[i, 1] + attObjCount * def[attObjID, i];
                }

                off_unit[attObjType, 1] = off_unit[attObjType, 1] + attObjCount * off[attObjID, 12] + attObjCount * off[attObjID, 3] + attObjCount * off[attObjID, 4] + attObjCount * off[attObjID, 5];
                def_unit[attObjType, 1] = def_unit[attObjType, 1] + attObjCount * def[attObjID, 12] + attObjCount * def[attObjID, 3] + attObjCount * def[attObjID, 4] + attObjCount * def[attObjID, 5];

                if (attReturning == 1)
                {
                    cancel = true;
                }

            }


            long defID = fBaseToUser(attTargetBaseID);

            //'Update defender resources
            long resStoreLevel = resStoreLvl(defID, attTargetBaseID);
            long basRes = bas_res(defID, attTargetBaseID, decimal.Round((Convert.ToDecimal(resInc(defID, attTargetBaseID))) / 60, 3), bas_cost(defID, attTargetBaseID), resSpace(resStoreLevel));
            //long basRes = 0;

            //first close the previous reader
            RecSetAtt2.Close();

            //'Defender
            query = "SELECT uObjID, uObjCount, uObjLevel FROM tbluobj WHERE uObjBaseID = " + attTargetBaseID + " AND uObjuID <> " + attUserID;

            cmdRecSetAtt2.CommandText = query;
            RecSetAtt2 = cmdRecSetAtt2.ExecuteReader();

            long uObjID;
            long uObjCount;
            long uObjType;

            while (RecSetAtt2.Read())
            {

                uObjID = Convert.ToInt64(RecSetAtt2["uObjID"]);
                uObjCount = Convert.ToInt64(RecSetAtt2["uObjCount"]);
                uObjType = arrObjType[uObjID];
                arrObjLevel[uObjID, 2] = Convert.ToInt64(RecSetAtt2["uObjLevel"]);
                if (uObjType == 1 || uObjType == 2)
                {
                    uObjType = 12;
                }
                units[uObjID, 2] = units[uObjID, 2] + uObjCount;
                units_type[uObjType, 2] = units_type[uObjType, 2] + uObjCount;
                if (!(uObjType == 12))
                {
                    xp[1] = xp[1] + (unit_xp[uObjID] * uObjCount);
                }

                for (i = 3; i <= 6; i++)
                {
                    if (i == 6)
                    {
                        i = 12;
                    }
                    off_army[i, 2] = off_army[i, 2] + uObjCount * off[uObjID, i];
                    def_army[i, 2] = def_army[i, 2] + uObjCount * def[uObjID, i];
                }

                off_unit[uObjType, 2] = off_unit[uObjType, 2] + uObjCount * off[uObjID, 12] + uObjCount * off[uObjID, 3] + uObjCount * off[uObjID, 4] + uObjCount * off[uObjID, 5];
                def_unit[uObjType, 2] = def_unit[uObjType, 2] + uObjCount * def[uObjID, 12] + uObjCount * def[uObjID, 3] + uObjCount * def[uObjID, 4] + uObjCount * def[uObjID, 5];


            }

            //'Create reference point
            //'Takes total attack value of 1 Soldier
            for (i = 3; i <= 6; i++)
            {
                if (i == 6)
                {
                    i = 12;
                }
                reference = reference + off[cSoldierID, i];
            }

            long valveMoney = valveFromBase(attTargetBaseID);
            //If IsNull(valveMoney) Or valveMoney = "" Or IsEmpty(valveMoney) Then valveMoney = 0

            //first close the previous reader
            RecSetAtt2.Close();
            query = "SELECT basDefDist, basDefAggr, basDefMob, basMoney, basHQ FROM tblbase WHERE basID = " + attTargetBaseID;
            cmdRecSetAtt2.CommandText = query;
            RecSetAtt2 = cmdRecSetAtt2.ExecuteReader();

            double defDist = 0;
            double defAggr = 0;
            double defMob = 0;
            long basHQ = 0;
            long basMoney = 0;
            if (RecSetAtt2.HasRows)
            {
                defDist = Convert.ToDouble(RecSetAtt2["basDefDist"]);
                defAggr = Convert.ToDouble(RecSetAtt2["basDefAggr"]);
                defMob = Convert.ToDouble(RecSetAtt2["basDefMob"]);

                basHQ = Convert.ToInt64(RecSetAtt2["basHQ"]);
                basMoney = Convert.ToInt64(RecSetAtt2["basMoney"]);

                if (basMoney < valveMoney)
                {
                    valveMoney = basMoney;
                }
                basMoney = basMoney - valveMoney;
            }
            else
            {
                //Console.WriteLine("Error: " + query);
            }


            //'off_unit(i,player) = hur bra players unit-typ i är mot alla andra trupper (attack)
            //'off_army(i,player) = hur bra players armé är mot unit-typ i (attack)
            //'def_unit(i,player) = hur bra players unit-typ i är mot alla andra trupper (försvar)
            //'def_army(i,player) = hur bra players armé är mot unit-typ i (försvar)

            //'Response.write("Attack results<br /><br />")


            //'Base defense bonus
            decimal defBonusTemp = basDefBonus(defID, attTargetBaseID) / 100;
            decimal defBonus = Math.Round(defBonusTemp);

            //'Attack plan vs Defense plan bonus
            long distBonus = fParamUserBonus("dist", attDist, defDist);
            long aggrBonus = fParamUserBonus("aggr", attAggr, defAggr);
            long mobBonus = fParamUserBonus("mob", attMob, defMob);

            bonus[distBonus] = bonus[distBonus] + float.Parse(Convert.ToString(Math.Abs(attDist - defDist))) * cTotParamBonus;
            bonus[aggrBonus] = bonus[aggrBonus] + float.Parse(Convert.ToString(Math.Abs(attAggr - defAggr))) * cTotParamBonus;
            bonus[mobBonus] = bonus[mobBonus] + float.Parse(Convert.ToString(Math.Abs(attMob - defMob))) * cTotParamBonus;

            bonus[2] = float.Parse(Convert.ToString(decimal.Add(Convert.ToDecimal(bonus[2]), defBonus))); //'Add base defense bonus

            bonus[1] = float.Parse(Convert.ToString(Math.Round(bonus[1], 4))) + 1;
            bonus[2] = float.Parse(Convert.ToString(Math.Round(bonus[2], 4))) + 1;
            //'response.write "def user bonus: " & Round(bonus(2),3)

            decimal attacker;
            decimal defender;
            long winner;
            long loser = 0;
            string sim_msg = "";
            decimal attacker_total = 0;
            decimal defender_total = 0;
            decimal val1;
            decimal val2;
            for (i = 3; i <= 6; i++)
            {
                if (i == 6)
                {
                    i = 12;
                }

                //'Calculate how many that survives of the unit type
                attacker = Convert.ToDecimal(Math.Round(((off_unit[i, 1] + off_army[i, 1]) * bonus[1]), 0));
                defender = Convert.ToDecimal(Math.Round(((def_army[i, 2] + def_unit[i, 2]) * bonus[2]), 0));

                //'Calculate winner
                winner = Convert.ToInt64(calc_winner(attacker, defender, "num"));
                loser = Convert.ToInt64(calc_winner(defender, attacker, "num"));

                if (i == 12)
                {
                    sim_msg = sim_msg + "defender = " + defender + "<br>";
                }
                //'Print
                //'Response.Write("a unit type " & i & ": " & off_unit(i,1) & " vs " & def_army(i,2) & " (" & attacker & "% " & a_winner & " units survives)<br />")
                //'Response.Write("Attacker unit type " & i & ": " & off_unit(i,2) & " vs " & def_army(i,1) & " (" & defender & "% " & d_winner & " units survives)<br />")
                //'Response.Write("c unit type " & i & ": " & off_army(i,1) & " vs " & def_unit(i,2) & " (" & attacker2 & "% " & a_winner2 & " units survives)<br /><br />")
                //'Response.Write("Defender unit type " & i & ": " & off_army(i,2) & " vs " & def_unit(i,1) & " (" & defender2 & "% " & d_winner2 & " units survives)<br /><br />")

                //'Response.Write("Troop type " & i & ": " & calc_winner(attacker,defender,"name") & " wins and loses " & _
                //'Round(100 - calc_winner_survivors(attacker,defender)*100,3) & "% of his troops. (" & attacker & " vs " & defender & ")<br /><br />")
                attacker_total = attacker_total + attacker;
                defender_total = defender_total + defender;

                if (winner == 1)
                {
                    val1 = attacker;
                    val2 = defender;
                }
                else
                {
                    val1 = defender;
                    val2 = attacker;
                }

                losses_type[i, winner] = Convert.ToInt64(Math.Round(Math.Round(1 - calc_winner_survivors(val1, val2), 5) * units_type[i, winner], 0));
                losses_type[i, loser] = Convert.ToInt64(Math.Round(Math.Round(1 - calc_winner_survivors(val1, val2), 5) * units_type[i, loser], 0));
                //'Response.write units_type(i,winner) & " - " & losses_type(i,winner) & "<br>"
            }
            long winner_total;
            string winner_total_display;
            winner_total_display = calc_winner(attacker_total, defender_total, "name");
            //'Response.write("The total winner is: " & winner_total_display & "<br><br>")
            winner_total = Convert.ToInt64(calc_winner(attacker_total, defender_total, "num"));

            sim_msg = sim_msg + "losses_type(12,loser) = " + losses_type[12, loser] + "<br>";
            sim_msg = sim_msg + "def_army(12,2) = " + def_army[12, 2] + "<br>";
            sim_msg = sim_msg + "def_unit(12,2) = " + def_unit[12, 2] + "<br>";
            sim_msg = sim_msg + "off_army(3,1) = " + off_army[3, 1] + "<br>";
            sim_msg = sim_msg + "off_unit(3,1) = " + off_army[3, 1] + "<br>";
            sim_msg = sim_msg + "ref = " + reference + "<br>";
            sim_msg = sim_msg + "attacker_total = " + attacker_total + "<br>";


            //'Any defender survivors? Then no building losses
            if (units_type[3, 2] > losses_type[3, 2] || units_type[4, 2] > losses_type[4, 2] || units_type[5, 2] > losses_type[5, 2])
            {
                losses_type[12, 2] = 0;
            }

            //'Attacker money
            long attMoneyGain = Convert.ToInt64(decimal.Round((attacker_total - defender_total) * cMoneyScaler * cOilValue, 0));
            if (attMoneyGain > basMoney)
            {
                attMoneyGain = basMoney;
            }
            if (attMoneyGain < 0)
            {
                attMoneyGain = 0;
            }

            //'If standard attack, no money gain
            if (attType == 1)
            {
                attMoneyGain = 0;
            }
            //'If loot attack, no buildings destroyed
            if (attType == 0)
            {
                losses_type[12, 2] = 0;
            }

            //'If general in attack force, no buildings destroyed
            if (winner_total == 1 && units[cGeneralID, 1] > 0 && basHQ == 0 && attType == 1)
            {
                losses_type[12, 2] = 0;
                sim_msg = sim_msg + "general in force, no building losses<br>";
            }


            sim_msg = sim_msg + "attack type: " + attType + "<br>";
            sim_msg = sim_msg + "building losses: " + losses_type[12, winner_total] + "<br>";


            long id = 0;
            Random Rnd = new Random();
            long listid;
            //for redim preserve
            long[] tempArray = new long[idsInCat.GetUpperBound(0) + 1];
            //'Calculate losses at winning team
            for (i = 3; i <= 6; i++)
            {
                if (i == 6)
                {
                    i = 12;
                }
                //'List possible types
                //ReDim idsInCat(0);
                idsInCat = new long[1];
                tempArray = new long[idsInCat.GetUpperBound(0) + 1];
                for (int i2 = 0; i2 <= cNumObjects; i2++)
                {
                    if (isBuilding(arrObjType[i2]) == i && units[i2, winner_total] > 0)
                    {
                        Array.Copy(idsInCat, tempArray, Math.Min(idsInCat.Length, tempArray.Length));
                        idsInCat = new long[idsInCat.GetUpperBound(0) + 1 + 1];
                        Array.Copy(tempArray, idsInCat, Math.Min(idsInCat.Length, tempArray.Length));
                        tempArray = new long[idsInCat.GetUpperBound(0) + 1];
                        idsInCat[idsInCat.GetUpperBound(0) + 1 - 1] = i2;
                        //'response.write(idsInCat(Ubound(idsInCat)-1) & ",")
                    }
                }

                for (int i3 = 1; i3 <= losses_type[i, winner_total]; i3++)
                {
                    if ((idsInCat.GetUpperBound(0) + 1) > -1)
                    {
                        id = 0;
                        listid = Convert.ToInt64(Rnd.NextDouble() * idsInCat.GetUpperBound(0));
                        //'response.write(listid & "/" & ubound(idsInCat) & "<br>")
                        id = idsInCat[listid];
                        losses[id, winner_total] = losses[id, winner_total] + 1;
                        //'response.write "Losses id " & id & ": +1 :" & units(id,winner_total)-losses(id,winner_total) & " -" & losses_type(i,winner_total) & "vs" & i3 & "<br>"
                        if (arrObjType[id] == 12)
                        {
                            if (Convert.ToInt64(arrObjLevel[id, 2] - (losses[id, winner_total])) <= 0)
                            {
                                arrdelete(ref idsInCat, listid);
                                //'response.write("ID " & id & ": " & (losses(id,winner_total)) & "<br>")
                            }
                        }
                        else
                        {
                            if (Convert.ToInt64(units[id, winner_total] - (losses[id, winner_total])) <= 0)
                            {
                                arrdelete(ref idsInCat, listid);
                            }
                        }
                    }
                }
            }



            //'Calculate building losses if defender loses
            if (winner_total == 1 && attType == 1 && units[cGeneralID, 1] == 0)
            {
                //'List possible types
                idsInCat = new long[1];
                tempArray = new long[idsInCat.GetUpperBound(0) + 1];
                for (int i2 = 0; i2 <= cNumObjects; i2++)
                {
                    if (isBuilding(arrObjType[i2]) == 12 && units[i2, 2] > 0)
                    {
                        Array.Copy(idsInCat, tempArray, Math.Min(idsInCat.Length, tempArray.Length));
                        idsInCat = new long[idsInCat.GetUpperBound(0) + 1 + 1];
                        Array.Copy(tempArray, idsInCat, Math.Min(idsInCat.Length, tempArray.Length));
                        tempArray = new long[idsInCat.GetUpperBound(0) + 1];
                        idsInCat[idsInCat.GetUpperBound(0) - 1] = i2;
                        //'response.write(idsInCat(Ubound(idsInCat)-1) & ",")
                    }
                }
                long attSurvivor;
                long attSurvivor_total = 0;
                for (i = 3; i <= 6; i++)
                {
                    if (i == 6)
                    {
                        i = 12;
                    }
                    attSurvivor = 0;
                    //'Calculate how many that survives of the unit type
                    if (units_type[i, 1] > 0)
                    {
                        attSurvivor = Convert.ToInt64(Math.Round((off_unit[i, 1] + off_army[i, 1]) * bonus[1] * ((units_type[i, 1] - losses_type[i, 1]) / units_type[i, 1]), 0));
                    }
                    else
                    {
                        attSurvivor = Convert.ToInt64(Math.Round((off_unit[i, 1] + off_army[i, 1]) * bonus[1], 0));
                    }

                    //'If units_type(i,1) > 0 Then sim_msg = sim_msg & " attSurvivor = " & attSurvivor

                    attSurvivor_total = attSurvivor_total + attSurvivor;
                }
                sim_msg = sim_msg + "<br>attSurvivor_total = " + attSurvivor_total + "<br>";

                //'defender * 10 vs attacker_total
                //'defender * 10 vs ref * 30 = 1
                //'losses_type(12,2)
                reference2 = ((reference * 6 * Convert.ToDecimal(Math.Round((def_army[12, 2] + def_unit[12, 2] * 0.01) * bonus[2], 0))));
                if (reference2 > 0)
                {
                    reference2 = 1 / reference2;
                }

                sim_msg = sim_msg + "ref2 = " + reference2 + "<br>";
                losses_type[12, 2] = Convert.ToInt64(Math.Round(reference2 * attSurvivor_total, 0));
                sim_msg = sim_msg + "losses_type(12,2) = " + losses_type[12, 2] + "<br>";
                if (losses_type[12, 2] > 5)
                {
                    losses_type[12, 2] = 5; //'Max 5 building levels destroyed
                }

                //'Calculate total number of surviving attackers
                //'numSurvivors = (units_type(3,1) - losses_type(3,1)) + (units_type(4,1) - losses_type(4,1)) + (units_type(5,1) - losses_type(5,1))
                //'sim_msg = sim_msg & units_type(12,2) & "<br>"
                //'losses_type(12,2) = numSurvivors / 277 '277 = 5000/18 (it takes 5000 survivors to destroy 18 buildings)


                for (int i3 = 1; i3 <= losses_type[12, 2]; i3++)
                {
                    if ((idsInCat.GetUpperBound(0)) > -1)
                    {
                        id = 0;
                        listid = Convert.ToInt64(Rnd.NextDouble() * (idsInCat.GetUpperBound(0)));
                        //'response.write(listid & "/" & ubound(idsInCat) & "<br>")
                        id = idsInCat[listid];
                        losses[id, 2] = losses[id, 2] + 1;
                        //'response.write "Losses id " & id & ": +1 :" & units(id,winner_total)-losses(id,winner_total) & " -" & losses_type(i,winner_total) & "vs" & i3 & "<br>"
                        if (arrObjType[id] == 12)
                        {
                            if (Convert.ToInt64(arrObjLevel[id, 2] - (losses[id, 2])) <= 0)
                            {
                                arrdelete(ref idsInCat, listid);
                            }
                            //'response.write("ID " & id & ": " & (losses(id,winner_total)) & "<br>")
                        }
                        else
                        {
                            if (Convert.ToInt64(units[id, 2] - (losses[id, 2])) <= 0)
                            {
                                arrdelete(ref idsInCat, listid);
                            }
                        }
                    }
                }

            }


            //'No admin building losses!
            losses[cAdminBuildingID, 2] = 0;

            //'If attacker is the winner, then no general losses!
            if (winner_total == 1)
            {
                losses[cGeneralID, 1] = 0;
            }

            //'PRINT LOSSES
            //'Heading and tables
            outprint = outprint + "<table border='0' width='500'><tr><td><h1>" + text[286] + "</h1>" + "</td></tr><tr><td><table border='0' rules='none' frame='box'><tr>";
            string title = fBaseName(attBaseID) + " " + text[413] + " " + fBaseName(attTargetBaseID);
            string attTypeText;
            if (attType == 0)
            {
                attTypeText = text[584];
            }
            else
            {
                attTypeText = text[583];
            }
            outprint = outprint + "<tr><td><strong>" + text[246] + "</strong></td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + title + "</td></tr>";
            outprint = outprint + "<tr><td><strong>" + text[287] + "</strong></td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + fLocalTime(attArrTime, cHourDiff).ToString("yyyy-MM-dd HH:mm:ss") + "</td></tr>";
            outprint = outprint + "<tr><td><strong>" + text[582] + "</strong></td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + attTypeText + "</td></tr><tr><td><br /><br /></td></tr>";

            outprint = outprint + "<tr><td></td></tr>";

            //close previous reader
            RecSetAtt.Close();
            //'List all units and how many that are in the current base
            query = "SELECT objID, objName, objIcon FROM tblobjects";
            cmdRecSetAtt.CommandText = query;
            RecSetAtt = cmdRecSetAtt.ExecuteReader();


            long objCount = 0;
            long objID;

            if (!RecSetAtt.HasRows)
            {
                outprint = outprint + "<tr><td>" + text[238] + "</td></tr>";
            }
            else
            {

                //'ATTACKER

                outprint = outprint + "<tr><td><strong>" + text[288] + "</strong></td><td colspan='" + Convert.ToString(numUnits - 1) + "'><span class='class3'><a href='?p=user&id=" + attUserID + "'>" + fUserName(attUserID) + "</a></span> " + text[19] + " <span class='class3'><a href='?p=base&id=" + attBaseID + "'>" + fBaseName(attBaseID) + "</a></span></td></tr><tr><td>&nbsp;</td>";

                //'Icons

                string objName;
                string objIcon;
                while (RecSetAtt.Read())
                {
                    objID = Convert.ToInt64(RecSetAtt["objID"]);
                    objName = Convert.ToString(RecSetAtt["objName"]);
                    objIcon = Convert.ToString(RecSetAtt["objIcon"]);
                    if (objIcon == "")
                    {
                        objIcon = "no_img.gif";
                    }
                    if (units[objID, 1] > 0)
                    {
                        if (arrObjType[objID] > 2 && arrObjType[objID] < 12) //'No buildings
                        {
                            outprint = outprint + "<td><img title='" + objName + "' src='img/objects/ico/" + objIcon + "' width='25' height='25' /></td>";
                        }
                    }

                }


                //'List attacking units
                //close previous reader
                RecSetAtt.Close();
                RecSetAtt = cmdRecSetAtt.ExecuteReader();
                outprint = outprint + "</tr><tr><td>" + text[289] + "</td>";

                while (RecSetAtt.Read())
                {
                    objID = Convert.ToInt64(RecSetAtt["objID"]);
                    objName = Convert.ToString(RecSetAtt["objName"]);
                    objIcon = Convert.ToString(RecSetAtt["objIcon"]);
                    objCount = units[objID, 1];
                    if (string.IsNullOrEmpty(Convert.ToString(objCount)))
                    {
                        objCount = 0;
                    }
                    if (arrObjType[objID] > 2 && arrObjType[objID] < 12) //'No buildings
                    {
                        if (string.IsNullOrEmpty(Convert.ToString(objCount)))
                        {
                            objCount = 0;
                        }
                        if (objCount > 0)
                        {
                            outprint = outprint + "<td>" + objCount + "</td>";
                        }
                    }

                }


                //'List losses
                //close previous reader
                RecSetAtt.Close();
                RecSetAtt = cmdRecSetAtt.ExecuteReader();
                outprint = outprint + "</tr><tr><td>" + text[290] + "</td>";
                query_att1 = "";
                string info = "";
                long newCount = 0;
                string objLevel = "";
                while (RecSetAtt.Read())
                {
                    objID = Convert.ToInt64(RecSetAtt["objID"]);
                    objName = Convert.ToString(RecSetAtt["objName"]);
                    objIcon = Convert.ToString(RecSetAtt["objIcon"]);
                    if (winner_total == 2)
                    {
                        objCount = units[objID, 1];
                    }
                    else
                    {
                        objCount = losses[objID, 1];
                    }
                    if (string.IsNullOrEmpty(Convert.ToString(objCount)))
                    {
                        objCount = 0;
                    }
                    if (objCount < 0)
                    {
                        objCount = 0;
                    }
                    if (arrObjType[objID] > 2 && arrObjType[objID] < 12) //'No buildings
                    {
                        if (string.IsNullOrEmpty(Convert.ToString(objCount)))
                        {
                            objCount = 0;
                        }
                        if (units[objID, 1] > 0)
                        {
                            outprint = outprint + "<td>" + objCount + "</td>";
                        }
                        if (units[objID, 1] > 0)
                        {
                            newCount = Convert.ToInt64(units[objID, 1]) - objCount;

                            if (objCount >= units[objID, 1])
                            {
                                query_att1 = query_att1 + "DELETE FROM tblattack WHERE attObjID = " + objID + " AND attGroupID = '" + groupID + "';";
                                //'out = out & objCount & " vs " & units(objID,1)
                            }
                            else
                            {
                                TimeSpan ts = attArrTime - attDepTime;
                                DateTime retArrTime = DateTime.Now.AddSeconds(ts.Seconds);
                                query_att1 = query_att1 + "UPDATE tblattack SET attObjCount = " + newCount + ", attReturning = 1, attArrTime = '" + retArrTime.ToString("yyyy-MM-dd HH:mm:ss") + "' WHERE attObjID = " + objID + " AND attGroupID = '" + groupID + "';";
                            }
                        }
                    }
                    else
                    {
                        if (losses[objID, 1] > 0)
                        {
                            info = info + objName + " " + text[291] + " " + objLevel + "<br />";
                        }
                    }

                }

                //'Attacker loses all troops
                if (winner_total == 2)
                {
                    query_att1 = "DELETE FROM tblattack WHERE attGroupID = '" + groupID + "';";
                }
                double temp = Convert.ToDouble(bonus[1] - bonus[2]) + Convert.ToDouble(defBonus);
                info = info + "" + text[292] + " " + Math.Round(temp, 3) * 100 + "%<br />";

                if (winner_total == 1)
                {
                    info = info + text[412] + " $" + attMoneyGain + "<br />";
                }

                outprint = outprint + "</tr><tr><td>" + text[293] + "</td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + info + "</td></tr>";

                outprint = outprint + "<tr><td colspan='" + numUnits + "'>&nbsp;</td></tr>";


                //'DEFENDER
                info = "";
                //close previous reader
                RecSetAtt.Close();
                RecSetAtt = cmdRecSetAtt.ExecuteReader();
                outprint = outprint + "<tr><td><strong>" + text[294] + "</strong></td><td colspan='" + Convert.ToString(numUnits - 1) + "'><span class='class3'><a href='?p=user&id=" + defID + "'>" + fUserName(defID) + "</a></span> " + text[19] + " <span class='class3'><a href='?p=base&id=" + attTargetBaseID + "'>" + fBaseName(attTargetBaseID) + "</a></span></td></tr><tr><td>&nbsp;</td>";
                out_all = outprint;


                //'Icons
                while (RecSetAtt.Read())
                {
                    objID = Convert.ToInt64(RecSetAtt["objID"]);
                    objName = Convert.ToString(RecSetAtt["objName"]);
                    objIcon = Convert.ToString(RecSetAtt["objIcon"]);
                    if (objIcon == "")
                    {
                        objIcon = "no_img.gif";
                    }
                    if (units[objID, 2] > 0)
                    {
                        if (arrObjType[objID] > 2 && arrObjType[objID] < 12) //'No buildings
                        {
                            out_all = out_all + "<td><img title='" + objName + "' src='img/objects/ico/" + objIcon + "' width='25' height='25' /></td>";
                        }
                    }

                }



                //'List units in base
                //close previous reader
                RecSetAtt.Close();
                RecSetAtt = cmdRecSetAtt.ExecuteReader();
                out_all = out_all + "</tr><tr><td>" + text[289] + "</td>";
                while (RecSetAtt.Read())
                {
                    objID = Convert.ToInt64(RecSetAtt["objID"]);
                    objName = Convert.ToString(RecSetAtt["objName"]);
                    objIcon = Convert.ToString(RecSetAtt["objIcon"]);
                    objCount = units[objID, 2];
                    if (string.IsNullOrEmpty(Convert.ToString(objCount)))
                    {
                        objCount = 0;
                    }
                    if (objCount > 0)
                    {
                        if (arrObjType[objID] > 2 && arrObjType[objID] < 12) //'No buildings
                        {
                            if (string.IsNullOrEmpty(Convert.ToString(objCount)))
                            {
                                objCount = 0;
                            }
                            out_all = out_all + "<td>" + objCount + "</td>";
                        }
                    }

                }


                //'List losses
                //close previous reader
                RecSetAtt.Close();
                RecSetAtt = cmdRecSetAtt.ExecuteReader();
                out_all = out_all + "</tr><tr><td>" + text[290] + "</td>";
                long unitCount;
                long uObjuID = 0;
                query_def1 = "";
                long newlevel = 0;
                query_def2 = "";
                while (RecSetAtt.Read())
                {
                    objID = Convert.ToInt64((RecSetAtt["objID"]));
                    objName = Convert.ToString(RecSetAtt["objName"]);
                    objIcon = Convert.ToString(RecSetAtt["objIcon"]);
                    if (winner_total == 1)
                    {
                        if (arrObjType[objID] == 12)
                        {
                            objCount = losses[objID, 2];
                        }
                        else
                        {
                            objCount = units[objID, 2];
                        }
                    }
                    else
                    {
                        objCount = losses[objID, 2];
                    }
                    if (string.IsNullOrEmpty(Convert.ToString(objCount)))
                    {
                        objCount = 0;
                    }
                    if (arrObjType[objID] > 2 && arrObjType[objID] < 12) //'No buildings
                    {
                        if (string.IsNullOrEmpty(Convert.ToString(objCount)))
                        {
                            objCount = 0;
                        }
                        if (units[objID, 2] > 0)
                        {
                            out_all = out_all + "<td>" + objCount + "</td>";
                        }
                        if (units[objID, 2] > 0)
                        {
                            if (objCount >= units[objID, 2])
                            {
                                query_def1 = query_def1 + "DELETE FROM tbluobj WHERE uObjID = " + objID + " AND uObjBaseID = " + attTargetBaseID + ";";
                            }
                            else
                            {
                                query = "SELECT uObjCount, uObjuID FROM tbluobj WHERE uObjID = " + objID + " AND uObjBaseID = " + attTargetBaseID + " ORDER BY uObjCount DESC";
                                //close previous reader
                                RecSetAtt2.Close();
                                cmdRecSetAtt2.CommandText = query;
                                RecSetAtt2 = cmdRecSetAtt2.ExecuteReader();
                                unitCount = objCount;
                                if (RecSetAtt2.HasRows)
                                {
                                    while (unitCount > 0)
                                    {
                                        uObjCount = Convert.ToInt64(RecSetAtt2["uObjCount"]);
                                        uObjuID = Convert.ToInt64(RecSetAtt2["uObjuID"]);
                                        newCount = Math.Abs(uObjCount - unitCount);
                                        if (unitCount >= uObjCount)
                                        {
                                            query_def1 = query_def1 + "DELETE FROM tbluobj WHERE uObjID = " + objID + " AND uObjBaseID = " + attTargetBaseID + " AND uObjuID = " + uObjuID + " LIMIT 1;";
                                            unitCount = unitCount - uObjCount;
                                        }
                                        else
                                        {
                                            query_def1 = query_def1 + "UPDATE tbluobj SET uObjCount = " + newCount + " WHERE uObjID = " + objID + " AND uObjBaseID = " + attTargetBaseID + " AND uObjuID = " + uObjuID + " LIMIT 1;";
                                            unitCount = unitCount - uObjCount;
                                        }

                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        sim_msg = sim_msg + "losses(" + objID + ",2) = " + losses[objID, 2] + "<br>";
                        if (losses[objID, 2] > 0)
                        {
                            newlevel = arrObjLevel[objID, 2] - losses[objID, 2];
                            xp[1] = xp[1] + unit_xp[objID] * losses[objID, 2];
                            if (newlevel == 0)
                            {
                                info = info + objName + " " + text[295] + "<br />";
                                query_def2 = query_def2 + "DELETE FROM tbluobj WHERE uObjID = " + objID + " AND uObjBaseID = " + attTargetBaseID + " AND uObjLevel = " + losses[objID, 2] + " LIMIT 1;";
                            }
                            else
                            {
                                info = info + objName + " " + text[291] + " " + newlevel + "<br />";
                                query_def2 = query_def2 + "UPDATE tbluobj SET uObjLevel = " + newlevel + " WHERE uObjID = " + objID + " AND uObjBaseID = " + attTargetBaseID + " AND uObjLevel = " + arrObjLevel[objID, 2] + " LIMIT 1;";
                            }
                        }
                    }

                }




                //'Generate code
                code = Convert.ToDecimal(Math.Round(Rnd.NextDouble() * 99999999)) + 1;


                //'XP Scaling			
                xp[1] = Convert.ToInt64(Math.Round(xp[1] * cXpScaler, 0));
                xp[2] = Convert.ToInt64(Math.Round(xp[2] * cXpScaler, 0));
                if (xp[1] < 0)
                {
                    xp[1] = 0;
                }
                if (xp[2] < 0)
                {
                    xp[2] = 0;
                }


                //'Defender loses all troops - decrease admin building level
                query_conquerbase = "";
                string query_conquerybase = "";
                query_xp = "";
                string att_link = "";
                string def_link = "";

                if (winner_total == 1)
                {
                    //'General in attacker troop?
                    if (units[cGeneralID, 1] > 0 && basHQ == 0 && attType == 1)
                    {
                        newlevel = arrObjLevel[cAdminBuildingID, 2] - 1;
                        if (newlevel == 0)
                        {
                            //'Admin building destroyed
                            info = info + "" + text[296] + "" + "<br />";
                            query_def2 = "DELETE FROM tbluobj WHERE uObjID = " + cAdminBuildingID + " AND uObjBaseID = " + attTargetBaseID + ";";
                        }
                        else if (newlevel == -1)
                        {
                            //'Attacker conquers the base
                            info = info + fUserName(attUserID) + " " + text[297] + " " + fBaseName(attTargetBaseID) + "<br />";
                            query_conquerbase = "UPDATE tblbase SET basUserID = " + attUserID + " WHERE basID = " + attTargetBaseID;
                            query_conquerbase = query_conquerbase + "; UPDATE tbluobj SET uObjuID = " + attUserID + " WHERE uObjBaseID = " + attTargetBaseID;
                            query_conquerbase = query_conquerbase + "; UPDATE tblqueue SET quUserID = " + attUserID + " WHERE quType = 0 AND quObjBaseID = " + attTargetBaseID;
                            //'Cancel attackers other attacks against the base
                            query_conquerybase = query_conquerbase + "; UPDATE tblattack SET attReturning = 1 WHERE attUserID = " + attUserID + " AND attTargetBaseID = " + attTargetBaseID;
                        }
                        else
                        {
                            //'Admin building damaged
                            info = info + "" + text[298] + " " + newlevel + "<br />";
                            query_def2 = "UPDATE tbluobj SET uObjLevel = " + newlevel + " WHERE uObjID = " + cAdminBuildingID + " AND uObjBaseID = " + attTargetBaseID + ";";
                        }
                    }
                    query_def1 = "DELETE FROM tbluobj WHERE uObjBaseID = " + attTargetBaseID + " AND uObjLoc = 99" + ";";
                    //'Attacker gets XP				
                    query_xp = "UPDATE tblbase SET basXP = basXP + " + xp[1] + " WHERE basID = " + attBaseID;
                    outprint = out_all;
                    att_link = att_link + text[521] + ":<br /><a href='?p=report&id=" + groupID + "&code=" + code + "'>" + text[563] + "/?p=report&id=" + groupID + "&code=" + code + "</a>";
                }
                else
                {
                    //'Defender gets XP
                    query_xp = "UPDATE tblbase SET basXP = basXP + " + xp[2] + " WHERE basID = " + attTargetBaseID;
                    outprint = outprint + "<tr><td>" + text[289] + "</td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + text[299] + "</td></tr>";
                }

                def_link = def_link + text[521] + ":<br /><a href='?p=report&id=" + groupID + "&code=" + code + "'>" + text[563] + "/?p=report&id=" + groupID + "&code=" + code + "</a>";

                outprint = outprint + "</tr><tr><td>" + text[293] + "</td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + info + "</td></tr>";
                out_all = out_all + "</tr><tr><td>" + text[293] + "</td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + info + "</td></tr>";

                outprint = outprint + "</table></td></tr></table>";
                out_all = out_all + "</table></td></tr></table>";

                outprint = outprint + "<br />" + att_link;
                out_all = out_all + "<br />" + def_link;

            }


            //'Send home troops reinforcing the base that belongs to the attacker
            //'---------------
            //'Count
            query = "SELECT uObjCount, uObjID FROM tbluobj WHERE uObjuID = " + attUserID + " AND uObjBaseID = " + attTargetBaseID + " AND uObjLoc = 99";
            OdbcCommand cmdRecSet2 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader RecSet2 = cmdRecSet2.ExecuteReader();

            //For only initialization
            OdbcCommand cmdRecSet = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader RecSet = cmdRecSet.ExecuteReader();
            RecSet.Close();

            long objSpeed;
            long targetBaseID;
            long x;
            long y;
            long base_x;
            long base_y;
            double distance;
            double eta;
            DateTime arrivalTime;
            string query_sendhome = "";
            string query_sendhome_del = "";
            while (RecSet2.Read())
            {
                objCount = Convert.ToInt64(RecSet2["uObjCount"]);
                objID = Convert.ToInt64(RecSet2["uObjID"]);

                //'Speed
                query = "SELECT objSpeed FROM tblobjects WHERE objID = " + objID;
                cmdRecSet = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                RecSet = cmdRecSet.ExecuteReader();
                objSpeed = 0;
                if (RecSet.HasRows)
                {
                    objSpeed = Convert.ToInt64(RecSet["objSpeed"]);
                }
                //'Target base
                //Close previous reader
                RecSet.Close();
                query = "SELECT basID, basX, basY FROM tblbase WHERE basUserID = " + attUserID + " AND basHQ = 1";
                cmdRecSet.CommandText = query;
                RecSet = cmdRecSet.ExecuteReader();

                if (RecSet.HasRows)
                {
                    targetBaseID = Convert.ToInt64(RecSet["basID"]);
                    x = Convert.ToInt64(RecSet["basX"]);
                    y = Convert.ToInt64(RecSet["basY"]);

                    //'Retrieve current base x,y
                    base_x = getBaseXY(attTargetBaseID, "x");
                    base_y = getBaseXY(attTargetBaseID, "y");

                    //'Get target distance based on x,y
                    distance = getDistance(base_x, base_y, x, y);

                    //'Get ETA in seconds
                    if (objSpeed == 0)
                    {
                        //response.write "attid: " & groupID & " objID = " & objID
                        //Console.WriteLine("attid: " + groupID + " objID = " + objID);
                    }
                    eta = speedToTime(objSpeed, distance);

                    //'Calculate arrival time based on slowest unit type
                    arrivalTime = DateTime.Now.AddSeconds(eta);

                    //'Insert into queue
                    query_sendhome = query_sendhome + "INSERT INTO tblqueue(quUserID, quObjCount, quFinished, quObjBaseID, quObjID, quObjLoc, quType) VALUES('" + attUserID + "','" + objCount + "','" + arrivalTime.ToString("yyyy-MM-dd HH:mm:ss") + "','" + targetBaseID + "','" + objID + "','" + attTargetBaseID + "',1);";

                    //'Remove from userobjects
                    query_sendhome_del = query_sendhome_del + "DELETE FROM tbluobj WHERE uObjBaseID = " + attTargetBaseID + " AND uObjuID = " + attUserID + " AND uObjID = " + objID + ";";
                }

            }
            //'---------------



            //'Money
            long targetBaseMoney;
            long attackerMoney;
            string query_mon1 = "";
            string query_mon2 = "";
            if (winner_total == 1) //'Attacker wins, send money
            {
                targetBaseMoney = fBaseMoney(attTargetBaseID) - attMoneyGain + valveMoney;
                attackerMoney = fBaseMoney(attBaseID) + attMoneyGain;
                if (targetBaseMoney < 0)
                {
                    targetBaseMoney = 0;
                }
                if (attackerMoney < 0)
                {
                    attackerMoney = 0;
                }
                query_mon1 = "UPDATE tblbase SET basMoney = " + targetBaseMoney + " WHERE basID = " + attTargetBaseID;
                query_mon2 = "UPDATE tblbase SET basMoney = " + attackerMoney + " WHERE basID = " + attBaseID;
            }


            //'Stats
            string attacker_win = "";
            string defender_win = "";
            if (winner_total == 1)
            {
                losses_type[3, 2] = units_type[3, 2];
                losses_type[4, 2] = units_type[4, 2];
                losses_type[5, 2] = units_type[5, 2];
                attacker_win = "staAttacksWon = staAttacksWon + 1, ";
            }
            else
            {
                losses_type[3, 1] = units_type[3, 1];
                losses_type[4, 1] = units_type[4, 1];
                losses_type[5, 1] = units_type[5, 1];
                defender_win = "staDefensesWon = staDefensesWon + 1, ";
            }

            if (string.IsNullOrEmpty(Convert.ToString(losses_type[3, 2])))
            {
                losses_type[3, 2] = 0;
            }
            if (string.IsNullOrEmpty(Convert.ToString(losses_type[4, 2])))
            {
                losses_type[4, 2] = 0;
            }
            if (string.IsNullOrEmpty(Convert.ToString(losses_type[5, 2])))
            {
                losses_type[5, 2] = 0;
            }
            if (string.IsNullOrEmpty(Convert.ToString(losses_type[3, 1])))
            {
                losses_type[3, 1] = 0;
            }
            if (string.IsNullOrEmpty(Convert.ToString(losses_type[4, 1])))
            {
                losses_type[4, 1] = 0;
            }
            if (string.IsNullOrEmpty(Convert.ToString(losses_type[5, 1])))
            {
                losses_type[5, 1] = 0;
            }


            string query_attstat = "UPDATE tblstats SET staAttacks = staAttacks + 1, " + attacker_win + "staKills3 = staKills3 + " + losses_type[3, 2] + ", staKills4 = staKills4 + " + losses_type[4, 2] + ", staKills5 = staKills5 + " + losses_type[5, 2] + ", staLosses3 = staLosses3 + " + losses_type[3, 1] + ", staLosses4 = staLosses4 + " + losses_type[4, 1] + ", staLosses5 = staLosses5 + " + losses_type[5, 1] + " WHERE staUserID = " + attUserID;

            string query_defstat = "UPDATE tblstats SET staDefenses = staDefenses + 1, " + defender_win + "staKills3 = staKills3 + " + losses_type[3, 1] + ", staKills4 = staKills4 + " + losses_type[4, 1] + ", staKills5 = staKills5 + " + losses_type[5, 1] + ", staLosses3 = staLosses3 + " + losses_type[3, 2] + ", staLosses4 = staLosses4 + " + losses_type[4, 2] + ", staLosses5 = staLosses5 + " + losses_type[5, 2] + " WHERE staUserID = " + defID;


            //'Double-check that the attack hasn't already been done
            //No need to check if all successfully completed.
            //if (attExists(groupID))
            //{
            //    //'Attack is duplicate, cancel!
            //    cancel = true;
            //}



            //'EXECUTE ALL QUERIES TO UPDATE DB
            //'------------------------------------------------

            if (sim == 0)
            {
                TimeSpan ts = attArrTime - DateTime.Now;
                if (ts.Seconds <= 0) //'Only if time up
                {
                    if (cancel == false)
                    {
                        //'Save and archive report
                        query_rep = "INSERT INTO tblattreport(attRepID, attReport,attRepTime,attRepAttacker,attRepDefender,attRepCode) VALUES('" + groupID + "','" + out_all.Replace("'", "''") + "','" + attArrTime.ToString("yyyy-MM-dd HH:mm:ss") + "'," + attUserID + "," + defID + "," + code + ") ON DUPLICATE KEY UPDATE attReport = '" + out_all.Replace("'", "''") + "'";
                        cmdRecSetCommon.CommandText = query_rep;
                        cmdRecSetCommon.ExecuteNonQuery();

                        //'Send home
                        mExecute(ConnectToMySqlObj.myConn, query_sendhome);
                        mExecute(ConnectToMySqlObj.myConn, query_sendhome_del);

                        //'Save changes
                        mExecute(ConnectToMySqlObj.myConn, query_def1);
                        mExecute(ConnectToMySqlObj.myConn, query_def2);
                        mExecute(ConnectToMySqlObj.myConn, query_att1);
                        //mExecute(ConnectToMySqlObj.myConn, query_att2);


                        //'Send report to attacker
                        query_attrep = "INSERT INTO tblmail(mDate,mSubject,mMessage,mToID,mRead,mFromID) VALUES('" + attArrTime.ToString("yyyy-MM-dd HH:mm:ss") + "','" + title.Replace("'", "''") + "','" + outprint.Replace("'", "''") + "'," + attUserID + ",0,-1)";
                        cmdRecSetCommon.CommandText = query_attrep;
                        cmdRecSetCommon.ExecuteNonQuery();

                        //'Send report to defender
                        query_defrep = "INSERT INTO tblmail(mDate,mSubject,mMessage,mToID,mRead,mFromID) VALUES('" + attArrTime.ToString("yyyy-MM-dd HH:mm:ss") + "','" + title.Replace("'", "''") + "','" + out_all.Replace("'", "''") + "'," + defID + ",0,-1)";
                        cmdRecSetCommon.CommandText = query_defrep;
                        cmdRecSetCommon.ExecuteNonQuery();

                        //'Save stats
                        cmdRecSetCommon.CommandText = query_attstat;
                        cmdRecSetCommon.ExecuteNonQuery();

                        cmdRecSetCommon.CommandText = query_defstat;
                        cmdRecSetCommon.ExecuteNonQuery();

                        //'response.write(query_xp)
                        cmdRecSetCommon.CommandText = query_xp;
                        cmdRecSetCommon.ExecuteNonQuery();

                        //'Attack in  same alliance?
                        long allID = fUserAll(attUserID);
                        if (allID == fUserAll(defID))
                        {
                            //'Update mini-feed
                            query = "INSERT INTO tblallfeed(afAllID,afUserID,afEvent,afTime) VALUES(" + allID + "," + attUserID + ",5,'" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "')";
                            cmdRecSetCommon.CommandText = query;
                            cmdRecSetCommon.ExecuteNonQuery();
                        }

                        if (winner_total == 1) //'Attacker wins, send money
                        {
                            //response.write query_mon1 & "<br>"
                            //response.write query_mon2 & "<br><br>"
                            //Console.WriteLine(query_mon1 + "<br><br>");
                            //Console.WriteLine(query_mon2 + "<br><br>");

                            cmdRecSetCommon.CommandText = query_mon1;
                            cmdRecSetCommon.ExecuteNonQuery();

                            cmdRecSetCommon.CommandText = query_mon2;
                            cmdRecSetCommon.ExecuteNonQuery();

                            //'Adding to economy table 
                            fTransLog(attUserID, attBaseID, -7, attMoneyGain);
                            fTransLog(defID, attTargetBaseID, -8, -attMoneyGain);
                        }

                        //'Base conquered?
                        if (query_conquerbase.Length > 0)
                        {
                            mExecute(ConnectToMySqlObj.myConn, query_conquerbase);
                        }
                    }
                }
            }
            else
            {
                //'Print (for debug)
                //Console.WriteLine(out_all);
                //Console.WriteLine("<br>" + query_def1 + "<br><br>" + query_def2 + "<br><br>".Replace(";",";<br>"));
                //Console.WriteLine(query_att1 + "<br><br>" + query_att2 + "<br><br>".Replace(";",";<br>"));
                //Console.WriteLine(query_attrep + "<br>");
                //Console.WriteLine(query_defrep + "<br>");
                //Console.WriteLine(query_rep + "<br>");
                //Console.WriteLine(query_mon1 + "<br>");
                //Console.WriteLine(query_mon2 + "<br>");
                //Console.WriteLine(query_xp + "<br>");
                //Console.WriteLine(query_sendhome + "<br>");
                //Console.WriteLine(query_sendhome_del + "<br>");
                //Console.WriteLine(query_conquerbase + "<br>");
                //Console.WriteLine(query_attstat + "<br>");
                //Console.WriteLine(query_defstat + "<br>");
                //Console.WriteLine("winner total: " + winner_total + "<br>");
                //Console.WriteLine(sim_msg);

            }



            //'--------------------------------------------------




            RecSetAtt.Close();
            RecSetAtt2.Close();

            RecSet.Close();
            RecSet2.Close();



        }


        //'Delete an index from an array
        public void arrdelete(ref long[] ar, long idx)
        {
            long i;
            long ub;
            ub = ar.GetUpperBound(0) - 1;
            for (i = idx; i <= ub; i++)
            {
                ar[i] = ar[i + 1];
            }
            //for redim preserve
            long[] temp = new long[ar.GetUpperBound(0) + 1];
            Array.Copy(ar, temp, Math.Min(ar.Length, temp.Length));
            ar = new long[ub + 1];
            Array.Copy(temp, ar, Math.Min(ar.Length, temp.Length));
        }

        //End of Process Attack

        //Process Spy
        public void spy(long spyID)
        {

            //'vars (outprint means out)
            //'Dim vars
            long i;
            string out_def = "";
            bool outcome;
            string outprint = "";
            //'fixed arrays (RecSetAtt no need its for recoedset)
            long[,] off_army = new long[12 + 1, 2 + 1];
            long[,] off_unit = new long[12 + 1, 2 + 1];
            long[,] def_army = new long[12 + 1, 2 + 1];
            long[,] def_unit = new long[12 + 1, 2 + 1];
            long[,] units_type = new long[12 + 1, 2 + 1];
            long[] xp = new long[2 + 1];



            //'Dim dynamic arrays

            //'ReDim dynamic arrays with values
            long[] idsInCat = new long[1 + 1];
            long[,] off = new long[cNumObjects + 1, 12 + 1];
            long[,] def = new long[cNumObjects + 1, 12 + 1];
            long[] arrObjType = new long[cNumObjects + 1];
            string[] arrObjName = new string[cNumObjects + 1];
            string[] arrObjIcon = new string[cNumObjects + 1];
            long[,] units = new long[cNumObjects + 1, 2 + 1];
            long[,] arrObjLevel = new long[cNumObjects + 1, 2 + 1];


            //'Number of units
            long numUnits = fNumObjects("False");

            //'Get Off/Def values
            string query = "SELECT * FROM tblobjects";
            OdbcCommand cmdRecSetAtt = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader RecSetAtt = cmdRecSetAtt.ExecuteReader();

            while (RecSetAtt.Read())
            {
                off[Convert.ToInt64(RecSetAtt["objID"]), 12] = Convert.ToInt64(RecSetAtt["objOff12"]);
                off[Convert.ToInt64(RecSetAtt["objID"]), 3] = Convert.ToInt64(RecSetAtt["objOff3"]);
                off[Convert.ToInt64(RecSetAtt["objID"]), 4] = Convert.ToInt64(RecSetAtt["objOff4"]);
                off[Convert.ToInt64(RecSetAtt["objID"]), 5] = Convert.ToInt64(RecSetAtt["objOff5"]);
                def[Convert.ToInt64(RecSetAtt["objID"]), 12] = Convert.ToInt64(RecSetAtt["objDef12"]);
                def[Convert.ToInt64(RecSetAtt["objID"]), 3] = Convert.ToInt64(RecSetAtt["objDef3"]);
                def[Convert.ToInt64(RecSetAtt["objID"]), 4] = Convert.ToInt64(RecSetAtt["objDef4"]);
                def[Convert.ToInt64(RecSetAtt["objID"]), 5] = Convert.ToInt64(RecSetAtt["objDef5"]);
                arrObjType[Convert.ToInt64(RecSetAtt["objID"])] = Convert.ToInt64(RecSetAtt["objCat"]);
                arrObjName[Convert.ToInt64(RecSetAtt["objID"])] = Convert.ToString(RecSetAtt["objName"]);
                arrObjIcon[Convert.ToInt64(RecSetAtt["objID"])] = Convert.ToString(RecSetAtt["objIcon"]);

            }


            //'Attacker (tactical recon team)
            query = "SELECT * FROM tblspy WHERE spyID = '" + spyID + "'";
            OdbcCommand cmdRecSetAtt2 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader RecSetAtt2 = cmdRecSetAtt2.ExecuteReader();

            long spyBaseID = 0;
            long spyUserID = 0;
            long spyTargetBaseID = 0;
            DateTime spyDepTime = DateTime.Now;
            DateTime spyArrTime = DateTime.Now;
            long spyReturning = 0;
            long spyLevel = 0;

            if (RecSetAtt2.HasRows)
            {
                spyBaseID = Convert.ToInt64(RecSetAtt2["spyBaseID"]);
                spyUserID = Convert.ToInt64(RecSetAtt2["spyUserID"]);
                spyTargetBaseID = Convert.ToInt64(RecSetAtt2["spyTargetBaseID"]);
                spyDepTime = Convert.ToDateTime(RecSetAtt2["spyDepTime"]);
                spyArrTime = Convert.ToDateTime(RecSetAtt2["spyArrTime"]);
                spyReturning = Convert.ToInt64(RecSetAtt2["spyReturning"]);
                spyLevel = Convert.ToInt64(RecSetAtt2["spyLevel"]);
            }



            //'Defender
            //Close previous reader
            RecSetAtt2.Close();
            query = "SELECT uObjID, uObjCount, uObjLevel FROM tbluobj WHERE uObjBaseID = " + spyTargetBaseID;
            cmdRecSetAtt2.CommandText = query;
            RecSetAtt2 = cmdRecSetAtt2.ExecuteReader();

            long uObjID;
            long uObjCount;
            long uObjType;

            while (RecSetAtt2.Read())
            {
                uObjID = Convert.ToInt64(RecSetAtt2["uObjID"]);
                uObjCount = Convert.ToInt64(RecSetAtt2["uObjCount"]);
                uObjType = arrObjType[uObjID];
                arrObjLevel[uObjID, 2] = Convert.ToInt64(RecSetAtt2["uObjLevel"]);

                if (uObjType == 1 || uObjType == 2)
                {
                    uObjType = 12;
                }
                units[uObjID, 2] = units[uObjID, 2] + uObjCount;
                units_type[uObjType, 2] = units_type[uObjType, 2] + units[uObjID, 2];

                for (i = 3; i <= 6; i++)
                {
                    if (i == 6)
                    {
                        i = 12;
                    }
                    off_army[i, 2] = off_army[i, 2] + uObjCount * off[uObjID, i];
                    def_army[i, 2] = def_army[i, 2] + uObjCount * def[uObjID, i];
                }

                off_unit[uObjType, 2] = off_unit[uObjType, 2] + uObjCount * off[uObjID, 12] + uObjCount * off[uObjID, 3] + uObjCount * off[uObjID, 4] + uObjCount * off[uObjID, 5];
                def_unit[uObjType, 2] = def_unit[uObjType, 2] + uObjCount * def[uObjID, 12] + uObjCount * def[uObjID, 3] + uObjCount * def[uObjID, 4] + uObjCount * def[uObjID, 5];

            }


            long defID = fBaseToUser(spyTargetBaseID);
            long defender;
            long defender_total = 0;
            for (i = 3; i <= 6; i++)
            {
                if (i == 6)
                {
                    i = 12;
                }
                defender = def_army[i, 2] + def_unit[i, 2];
                defender_total = defender_total + defender;
            }

            long lvl = spyLevel;
            outcome = spyLvlVsBase(lvl, defender_total);


            //'PRINT RESULT
            //'Heading and tables
            outprint = outprint + "<table border='0'><tr><td><h1>" + text[286] + "</h1>" + "</td></tr><tr><td><table border='0' rules='none' frame='box'><tr>";
            string title = fBaseName(spyBaseID) + " " + text[572] + " " + fBaseName(spyTargetBaseID);
            outprint = outprint + "<tr><td><strong>" + text[569] + "</strong></td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + title + "</td></tr>";
            outprint = outprint + "<tr><td><strong>" + text[287] + "</strong></td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + fLocalTime(spyArrTime, cHourDiff).ToString("yyyy-MM-dd HH:mm:ss") + "</td></tr><tr><td><br /><br /></td></tr>";

            outprint = outprint + "<tr><td></td>";

            //'List all units and how many that are in the current base
            //Close previous reader
            RecSetAtt.Close();
            query = "SELECT objID, objName, objIcon FROM tblobjects";
            cmdRecSetAtt.CommandText = query;
            RecSetAtt = cmdRecSetAtt.ExecuteReader();

            long objID;
            string objName;
            string objIcon;
            long objCount;


            if (!RecSetAtt.HasRows)
            {
                outprint = outprint + "<tr><td>" + text[238] + "</td></tr>";
            }
            else
            {
                //'ATTACKER

                outprint = outprint + "<tr><td><strong>" + text[288] + "</strong></td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + fUserName(spyUserID) + " " + text[19] + " " + fBaseName(spyBaseID) + "</td></tr><tr><td>&nbsp;</td>";
                //'List attacking units
                outprint = outprint + "<td>" + arrObjName[cSpyID] + " " + text[18] + " " + spyLevel + "</td></tr>";

                //'Icons
                while (RecSetAtt.Read())
                {
                    objID = Convert.ToInt64(RecSetAtt["objID"]);
                    objName = Convert.ToString(RecSetAtt["objName"]);
                    objIcon = Convert.ToString(RecSetAtt["objIcon"]);

                    if (objIcon == "")
                    {
                        objIcon = "no_img.gif";
                    }
                    if (units[objID, 1] > 0)
                    {
                        if (arrObjType[objID] > 2 && arrObjType[objID] < 12) //'No buildings
                        {
                            outprint = outprint + "<td><img title='" + objName + "' src='img/objects/ico/" + objIcon + "' width='25' height='25' /></td>";
                        }
                    }

                }

                //'DEFENDER

                //close previous reader
                RecSetAtt.Close();
                RecSetAtt = cmdRecSetAtt.ExecuteReader();
                outprint = outprint + "<tr><td><strong>" + text[294] + "</strong></td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + fUserName(defID) + " " + text[19] + " " + fBaseName(spyTargetBaseID) + "</td></tr><tr><td>&nbsp;</td>";

                out_def = out_def + "<tr><table><tr>";

                //'Icons
                while (RecSetAtt.Read())
                {
                    objID = Convert.ToInt64(RecSetAtt["objID"]);
                    objName = Convert.ToString(RecSetAtt["objName"]);
                    objIcon = Convert.ToString(RecSetAtt["objIcon"]);
                    if (objIcon == "")
                    {
                        objIcon = "no_img.gif";
                    }
                    if (units[objID, 2] > 0)
                    {
                        if (arrObjType[objID] > 2 && arrObjType[objID] < 12)//'No buildings
                        {
                            out_def = out_def + "<td width='50'><img title='" + objName + "' src='img/objects/ico/" + objIcon + "' width='25' height='25' /></td>";
                        }
                    }

                }
                //'List units in base
                //close previous reader
                RecSetAtt.Close();
                RecSetAtt = cmdRecSetAtt.ExecuteReader();
                out_def = out_def + "</tr><tr>";
                bool basEmpty = true;
                string defensePlan;
                while (RecSetAtt.Read())
                {
                    objID = Convert.ToInt64(RecSetAtt["objID"]);
                    objName = Convert.ToString(RecSetAtt["objName"]);
                    objIcon = Convert.ToString(RecSetAtt["objIcon"]);
                    objCount = units[objID, 2];
                    if (string.IsNullOrEmpty(Convert.ToString(objCount)))
                    {
                        objCount = 0;
                    }
                    if (objCount > 0)
                    {
                        if (arrObjType[objID] > 2 && arrObjType[objID] < 12) //'No buildings
                        {
                            if (string.IsNullOrEmpty(Convert.ToString(objCount)))
                            {
                                objCount = 0;
                            }
                            out_def = out_def + "<td>" + objCount + "</td>";
                            basEmpty = false;
                        }
                    }

                }

                if (basEmpty == true)
                {
                    out_def = out_def + "<td width='200'>" + text[4] + "</td>";
                }

                out_def = out_def + "</tr></table></tr>";

                if (outcome == true)
                {
                    //'Determine defense plan
                    defensePlan = defPlan(spyTargetBaseID);

                    //'Print outcome
                    outprint = outprint + "<tr><td colspan='2'>" + text[570] + " " + arrObjName[cSpyID] + " " + text[567] + "</td></tr>";
                    outprint = outprint + "<tr><td colspan='2'>" + out_def + "</td></tr>";
                    outprint = outprint + "<tr><td colspan='2'><br /><b>" + text[573] + " </b></td></tr>";
                    outprint = outprint + "<tr><td colspan='2'>" + defensePlan + "</td></tr>";
                }
                else
                {
                    outprint = outprint + "<tr><td colspan='2'>" + text[570] + " " + arrObjName[cSpyID] + " " + text[568] + "</td></tr>";
                }

                outprint = outprint + "</table>";
            }



            //'EXECUTE ALL QUERIES TO UPDATE DB
            //'------------------------------------------------

            //'response.write out
            TimeSpan ts = spyArrTime - DateTime.Now;
            if (ts.Seconds <= 0) //'Only if time up
            {
                //'Send report to attacker
                string query_attrep = "INSERT INTO tblmail(mDate,mSubject,mMessage,mToID,mRead,mFromID) VALUES('" + spyArrTime.ToString("yyyy-MM-dd HH:mm:ss") + "','" + title.Replace("'", "''") + "','" + outprint.Replace("'", "''") + "'," + spyUserID + ",0,-1)";
                cmdRecSetCommon.CommandText = query_attrep;
                cmdRecSetCommon.ExecuteNonQuery();

                //'Send report to defender
                string query_defrep = "INSERT INTO tblmail(mDate,mSubject,mMessage,mToID,mRead,mFromID) VALUES('" + spyArrTime.ToString("yyyy-MM-dd HH:mm:ss") + "','" + title.Replace("'", "''") + "','" + outprint.Replace("'", "''") + "'," + defID + ",0,-1)";
                cmdRecSetCommon.CommandText = query_defrep;
                cmdRecSetCommon.ExecuteNonQuery();

                //'Send back spy or delete
                if (outcome == true)
                {
                    ts = spyDepTime - spyArrTime;
                    DateTime retArrTime = spyArrTime.AddSeconds(ts.Seconds);
                    query = "UPDATE tblspy SET spyReturning = 1, spyArrTime = '" + retArrTime.ToString("yyyy-MM-dd HH:mm:ss") + "' WHERE spyID = " + spyID;
                }
                else
                {
                    query = "DELETE FROM tblspy WHERE spyID = " + spyID;
                }
                cmdRecSetCommon.CommandText = query;
                cmdRecSetCommon.ExecuteNonQuery();

                //'Print (for debug)
                //'response.write(out)
            }


            RecSetAtt.Close();
            RecSetAtt2.Close();


        }

        //End of Process Spy

        //Process Missile Attack
        public void missile_attack(long id, long sim)
        {

            long i;
            string outprint = "";
            //'fixed arrays (RecSetAtt no need its for recoedset)
            long[,] off_army = new long[12 + 1, 2 + 1];
            long[,] off_unit = new long[12 + 1, 2 + 1];
            long[,] def_army = new long[12 + 1, 2 + 1];
            long[,] def_unit = new long[12 + 1, 2 + 1];
            long[,] units_type = new long[12 + 1, 2 + 1];
            double[,] losses_type = new double[12 + 1, 2 + 1];
            float[] bonus = new float[2 + 1];
            long[] xp = new long[2 + 1];
            //'Dim dynamic arrays
            //Dim idsInCat(), off(), def(), arrObjType(), arrObjName(), arrObjIcon(), units(), losses(), arrObjLevel(), unit_xp()
            //'ReDim dynamic arrays with values
            long[] idsInCat = new long[1 + 1];
            long[,] off = new long[cNumObjects + 1, 12 + 1];
            long[,] def = new long[cNumObjects + 1, 12 + 1];
            long[] arrObjType = new long[cNumObjects + 1];
            string[] arrObjName = new string[cNumObjects + 1];
            string[] arrObjIcon = new string[cNumObjects + 1];
            long[,] units = new long[cNumObjects + 1, 2 + 1];
            long[,] losses = new long[cNumObjects + 1, 2 + 1];
            long[,] arrObjLevel = new long[cNumObjects + 1, 2 + 1];
            long[] unit_xp = new long[cNumObjects + 1];


            bool cancel = false;

            long maID = id;

            //'Number of units
            long numUnits = fNumObjects("False");

            string out_all = "";
            decimal code = 0;
            string query_def1 = "";
            string query_def2 = "";
            long winner_total = 0;
            long newCount = 0;
            string sim_msg = "";
            string def_link = "";

            //'Get Off/Def values
            string query = "SELECT * FROM tblobjects";
            OdbcCommand cmdRecSetAtt = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader RecSetAtt = cmdRecSetAtt.ExecuteReader();
            while (RecSetAtt.Read())
            {
                arrObjType[Convert.ToInt64(RecSetAtt["objID"])] = Convert.ToInt64(RecSetAtt["objCat"]);
                arrObjName[Convert.ToInt64(RecSetAtt["objID"])] = Convert.ToString(RecSetAtt["objName"]);
                arrObjIcon[Convert.ToInt64(RecSetAtt["objID"])] = Convert.ToString(RecSetAtt["objIcon"]);
                unit_xp[Convert.ToInt64(RecSetAtt["objID"])] = Convert.ToInt64(RecSetAtt["objXP"]);

            }

            //'Attacker

            //'Attacker
            query = "SELECT * FROM tblmissileattack WHERE maID = " + maID;
            OdbcCommand cmdRecSetAtt2 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader RecSetAtt2 = cmdRecSetAtt2.ExecuteReader();

            long maBaseID = 0;
            long maUserID = 0;
            long maTargetBaseID = 0;
            long maType = 0;
            DateTime maSendTime = DateTime.Now;
            DateTime maArrTime = DateTime.Now;

            while (RecSetAtt2.Read())
            {
                maBaseID = Convert.ToInt64(RecSetAtt2["maBaseID"]);
                maUserID = Convert.ToInt64(RecSetAtt2["mauID"]);
                maTargetBaseID = Convert.ToInt64(RecSetAtt2["maTargetBaseID"]);
                maType = Convert.ToInt64(RecSetAtt2["maType"]);
                maSendTime = Convert.ToDateTime(RecSetAtt2["maSendTime"]);
                maArrTime = Convert.ToDateTime(RecSetAtt2["maArrTime"]);
            }


            long defID = fBaseToUser(maTargetBaseID);

            //'Update defender resources

            //'Update defender resources
            long resStoreLevel = resStoreLvl(defID, maTargetBaseID);
            long basRes = bas_res(defID, maTargetBaseID, decimal.Round((Convert.ToDecimal(resInc(defID, maTargetBaseID))) / 60, 3), bas_cost(defID, maTargetBaseID), resSpace(resStoreLevel));


            //'Defender
            //close previous reader
            RecSetAtt2.Close();
            query = "SELECT uObjID, uObjCount, uObjLevel FROM tbluobj WHERE uObjBaseID = " + maTargetBaseID + " AND uObjuID <> " + maUserID;

            cmdRecSetAtt2.CommandText = query;
            RecSetAtt2 = cmdRecSetAtt2.ExecuteReader();

            long uObjID;
            long uObjCount = 0;
            long uObjType;

            while (RecSetAtt2.Read())
            {

                uObjID = Convert.ToInt64(RecSetAtt2["uObjID"]);
                uObjCount = Convert.ToInt64(RecSetAtt2["uObjCount"]);
                uObjType = arrObjType[uObjID];
                arrObjLevel[uObjID, 2] = Convert.ToInt64(RecSetAtt2["uObjLevel"]);
                if (uObjType == 1 || uObjType == 2)
                {
                    uObjType = 12;
                }
                units[uObjID, 2] = units[uObjID, 2] + uObjCount;
                units_type[uObjType, 2] = units_type[uObjType, 2] + uObjCount;

            }


            // 'Determine losses depending on missile type
            long totNumGroundUnits = units_type[3, 2] + units_type[4, 2];
            long totNumAirUnits = units_type[5, 2];

            switch (maType)
            {
                case 1:
                    //'10% of ground units
                    losses_type[3, 2] = units_type[3, 2] * 0.1;
                    losses_type[4, 2] = units_type[4, 2] * 0.1;
                    losses_type[5, 2] = 0;
                    losses_type[12, 2] = 0;
                    break;
                case 2:
                    //'20% of ground units
                    losses_type[3, 2] = units_type[3, 2] * 0.2;
                    losses_type[4, 2] = units_type[4, 2] * 0.2;
                    losses_type[5, 2] = 0;
                    losses_type[12, 2] = 0;
                    break;
                case 3:
                    //'30% of ground units
                    losses_type[3, 2] = units_type[3, 2] * 0.3;
                    losses_type[4, 2] = units_type[4, 2] * 0.3;
                    losses_type[5, 2] = 0;
                    losses_type[12, 2] = 0;
                    break;
                case 4:
                    //'20% of ground units and 20% of air units
                    losses_type[3, 2] = units_type[3, 2] * 0.2;
                    losses_type[4, 2] = units_type[4, 2] * 0.2;
                    losses_type[5, 2] = units_type[5, 2] * 0.2;
                    losses_type[12, 2] = 0;
                    break;
                case 5:
                    //'Administration destroyed
                    losses_type[3, 2] = 0;
                    losses_type[4, 2] = 0;
                    losses_type[5, 2] = 0;
                    losses_type[12, 2] = 0;
                    losses[cAdminBuildingID, 2] = arrObjLevel[cAdminBuildingID, 2];
                    break;
            }


            //'Randomize losses
            Random Rnd = new Random();
            long listid;
            string info = "";

            //for redim preserve
            long[] temp = new long[idsInCat.GetUpperBound(0) + 1];


            for (i = 3; i <= 5; i++)
            {
                //'List possible types
                idsInCat = new long[1];
                temp = new long[idsInCat.GetUpperBound(0) + 1];
                for (int i2 = 0; i2 <= cNumObjects; i2++)
                {
                    if (isBuilding(arrObjType[i2]) == i && units[i2, 2] > 0)
                    {
                        Array.Copy(idsInCat, temp, Math.Min(idsInCat.Length, temp.Length));
                        idsInCat = new long[idsInCat.GetUpperBound(0) + 1 + 1];
                        Array.Copy(temp, idsInCat, Math.Min(idsInCat.Length, temp.Length));
                        temp = new long[idsInCat.GetUpperBound(0) + 1];
                        idsInCat[idsInCat.GetUpperBound(0) - 1] = i2;
                    }
                }
                for (int i3 = 1; i3 <= losses_type[i, 2]; i3++)
                {
                    if ((idsInCat.GetUpperBound(0) + 1) > -1)
                    {
                        id = 0;
                        listid = Convert.ToInt64(Rnd.NextDouble() * idsInCat.GetUpperBound(0));
                        id = idsInCat[listid];
                        losses[id, 2] = losses[id, 2] + 1;
                        if (Convert.ToInt64(units[id, 2] - (losses[id, 2])) <= 0)
                        {
                            arrdelete(ref idsInCat, listid);
                        }
                    }
                }
            }


            //'PRINT LOSSES
            //'Heading and tables
            outprint = outprint + "<table border='0' width='500'><tr><td><h1>" + text[286] + "</h1>" + "</td></tr><tr><td><table border='0' rules='none' frame='box'><tr>";
            string title = fBaseName(maBaseID) + " " + text[413] + " " + fBaseName(maTargetBaseID);

            outprint = outprint + "<tr><td><strong>" + text[246] + "</strong></td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + title + "</td></tr>";
            outprint = outprint + "<tr><td><strong>" + text[287] + "</strong></td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + fLocalTime(maArrTime, cHourDiff).ToString("yyyy-MM-dd HH:mm:ss") + "</td></tr><tr><td><br /><br /></td></tr>";

            outprint = outprint + "<tr><td></td></tr>";

            //'List all units and how many that are in the current base
            //close previous reader
            RecSetAtt.Close();
            //'List all units and how many that are in the current base
            query = "SELECT objID, objName, objIcon FROM tblobjects";
            cmdRecSetAtt.CommandText = query;
            RecSetAtt = cmdRecSetAtt.ExecuteReader();

            long objCount = 0;
            long objID = 0;

            if (!RecSetAtt.HasRows)
            {
                outprint = outprint + "<tr><td>" + text[238] + "</td></tr>";
            }
            else
            {

                //'ATTACKER
                outprint = outprint + "<tr><td><strong>" + text[288] + "</strong></td><td colspan='" + Convert.ToString(numUnits - 1) + "'><span class='class3'><a href='?p=user&id=" + maUserID + "'>" + fUserName(maUserID) + "</a></span> " + text[19] + " <span class='class3'><a href='?p=base&id=" + maBaseID + "'>" + fBaseName(maBaseID) + "</a></span></td></tr><tr><td>&nbsp;</td>";
                outprint = outprint + "<tr><td colspan='" + numUnits + "'><img src='img/objects/missile-" + maType + ".gif' border='0' /></td></tr>";
                outprint = outprint + "<tr><td colspan='" + numUnits + "'>&nbsp;</td></tr>";

                //'DEFENDER
                info = "";
                outprint = outprint + "<tr><td><strong>" + text[294] + "</strong></td><td colspan='" + Convert.ToString(numUnits - 1) + "'><span class='class3'><a href='?p=user&id=" + defID + "'>" + fUserName(defID) + "</a></span> " + text[19] + " <span class='class3'><a href='?p=base&id=" + maTargetBaseID + "'>" + fBaseName(maTargetBaseID) + "</a></span></td></tr><tr><td>&nbsp;</td>";
                out_all = outprint;

                //'Icons

                string objName;
                string objIcon;
                while (RecSetAtt.Read())
                {
                    objID = Convert.ToInt64(RecSetAtt["objID"]);
                    objName = Convert.ToString(RecSetAtt["objName"]);
                    objIcon = Convert.ToString(RecSetAtt["objIcon"]);
                    if (objIcon == "")
                    {
                        objIcon = "no_img.gif";
                    }
                    if (units[objID, 2] > 0)
                    {
                        if (arrObjType[objID] > 2 && arrObjType[objID] < 12) //'No buildings
                        {
                            out_all = out_all + "<td><img title='" + objName + "' src='img/objects/ico/" + objIcon + "' width='25' height='25' /></td>";
                        }
                    }

                }


                //'List units in base
                //close previous reader
                RecSetAtt.Close();
                RecSetAtt = cmdRecSetAtt.ExecuteReader();
                out_all = out_all + "</tr><tr><td>" + text[289] + "</td>";
                while (RecSetAtt.Read())
                {
                    objID = Convert.ToInt64(RecSetAtt["objID"]);
                    objName = Convert.ToString(RecSetAtt["objName"]);
                    objIcon = Convert.ToString(RecSetAtt["objIcon"]);
                    objCount = units[objID, 2];
                    if (string.IsNullOrEmpty(Convert.ToString(objCount)))
                    {
                        objCount = 0;
                    }
                    if (objCount > 0)
                    {
                        if (arrObjType[objID] > 2 && arrObjType[objID] < 12) //'No buildings
                        {
                            if (string.IsNullOrEmpty(Convert.ToString(objCount)))
                            {
                                objCount = 0;
                            }
                            out_all = out_all + "<td>" + objCount + "</td>";
                        }
                    }

                }

                //'List losses
                //close previous reader
                RecSetAtt.Close();
                RecSetAtt = cmdRecSetAtt.ExecuteReader();
                out_all = out_all + "</tr><tr><td>" + text[290] + "</td>";
                long unitCount = objCount;
                long uObjuID = 0;
                query_def1 = "";
                long newlevel = 0;
                query_def2 = "";
                while (RecSetAtt.Read())
                {
                    objID = Convert.ToInt64((RecSetAtt["objID"]));
                    objName = Convert.ToString(RecSetAtt["objName"]);
                    objIcon = Convert.ToString(RecSetAtt["objIcon"]);
                    if (winner_total == 1)
                    {
                        if (arrObjType[objID] == 12)
                        {
                            objCount = losses[objID, 2];
                        }
                        else
                        {
                            objCount = units[objID, 2];
                        }
                    }
                    else
                    {
                        objCount = losses[objID, 2];
                    }
                    if (string.IsNullOrEmpty(Convert.ToString(objCount)))
                    {
                        objCount = 0;
                    }

                    if (arrObjType[objID] > 2 && arrObjType[objID] < 12) //'No buildings
                    {
                        if (string.IsNullOrEmpty(Convert.ToString(objCount)))
                        {
                            objCount = 0;
                        }
                        if (units[objID, 2] > 0)
                        {
                            out_all = out_all + "<td>" + objCount + "</td>";
                        }
                        if (units[objID, 2] > 0)
                        {
                            if (objCount >= units[objID, 2])
                            {
                                query_def1 = query_def1 + "DELETE FROM tbluobj WHERE uObjID = " + objID + " AND uObjBaseID = " + maTargetBaseID + ";";
                            }
                            else
                            {
                                query = "SELECT uObjCount, uObjuID FROM tbluobj WHERE uObjID = " + objID + " AND uObjBaseID = " + maTargetBaseID + " ORDER BY uObjCount DESC";
                                //close previous reader
                                RecSetAtt2.Close();
                                cmdRecSetAtt2.CommandText = query;
                                RecSetAtt2 = cmdRecSetAtt2.ExecuteReader();
                                unitCount = objCount;
                                while (unitCount > 0)
                                {
                                    while (RecSetAtt2.Read())
                                    {
                                        uObjCount = Convert.ToInt64(RecSetAtt2["uObjCount"]);
                                        uObjuID = Convert.ToInt64(RecSetAtt2["uObjuID"]);
                                    }
                                    newCount = Math.Abs(uObjCount - unitCount);
                                    if (unitCount >= uObjCount)
                                    {
                                        query_def1 = query_def1 + "DELETE FROM tbluobj WHERE uObjID = " + objID + " AND uObjBaseID = " + maTargetBaseID + " AND uObjuID = " + uObjuID + " LIMIT 1;";
                                        unitCount = unitCount - uObjCount;
                                    }
                                    else
                                    {
                                        query_def1 = query_def1 + "UPDATE tbluobj SET uObjCount = uObjCount - " + unitCount + " WHERE uObjID = " + objID + " AND uObjBaseID = " + maTargetBaseID + " AND uObjuID = " + uObjuID + " LIMIT 1;";
                                        unitCount = unitCount - uObjCount;
                                    }

                                }
                            }
                        }
                    }
                    else
                    {

                        sim_msg = sim_msg + "losses(" + objID + ",2) = " + losses[objID, 2] + "<br>";
                        if (losses[objID, 2] > 0)
                        {
                            newlevel = arrObjLevel[objID, 2] - losses[objID, 2];
                            if (newlevel == 0)
                            {
                                info = info + objName + " " + text[295] + "<br />";
                                query_def2 = query_def2 + "DELETE FROM tbluobj WHERE uObjID = " + objID + " AND uObjBaseID = " + maTargetBaseID + " AND uObjLevel = " + losses[objID, 2] + " LIMIT 1;";
                            }
                            else
                            {
                                info = info + objName + " " + text[291] + " " + newlevel + "<br />";
                                query_def2 = query_def2 + "UPDATE tbluobj SET uObjLevel = " + newlevel + " WHERE uObjID = " + objID + " AND uObjBaseID = " + maTargetBaseID + " AND uObjLevel = " + arrObjLevel[objID, 2] + " LIMIT 1;";
                            }
                        }
                    }

                }

                outprint = outprint + "<tr><td>" + text[293] + "</td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + info + "</td></tr>";
                out_all = out_all + "</tr><tr><td>" + text[293] + "</td><td colspan='" + Convert.ToString(numUnits - 1) + "'>" + info + "</td></tr>";

                outprint = outprint + "</table></td></tr></table>";
                out_all = out_all + "</table></td></tr></table>";

                //'Generate code
                code = Convert.ToDecimal(Math.Round(Rnd.NextDouble() * 99999999)) + 1;


                //'Report link
                def_link = def_link + text[521] + ":<br /><a href='?p=report&id=" + id + "&code=" + code + "'>" + text[563] + "/?p=report&id=" + id + "&code=" + code + "</a>";



            }

            //'Send home troops reinforcing the base that belongs to the attacker
            //'---------------
            //'Count
            query = "SELECT uObjCount, uObjID FROM tbluobj WHERE uObjuID = " + maUserID + " AND uObjBaseID = " + maTargetBaseID + " AND uObjLoc = 99";
            OdbcCommand cmdRecSet2 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader RecSet2 = cmdRecSet2.ExecuteReader();

            OdbcCommand cmdRecSet = new OdbcCommand("SELECT objSpeed FROM tblobjects WHERE objID = 0", ConnectToMySqlObj.myConn);
            OdbcDataReader RecSet = cmdRecSet.ExecuteReader();

            long objSpeed;
            long targetBaseID;
            long x;
            long y;
            long base_x;
            long base_y;
            double distance;
            double eta;
            DateTime arrivalTime;
            string query_sendhome = "";
            string query_sendhome_del = "";
            while (RecSet2.Read())
            {

                objCount = Convert.ToInt64(RecSet2["uObjCount"]);
                objID = Convert.ToInt64(RecSet2["uObjID"]);

                //'Speed
                query = "SELECT objSpeed FROM tblobjects WHERE objID = " + objID;
                cmdRecSet = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                RecSet = cmdRecSet.ExecuteReader();

                objSpeed = Convert.ToInt64(RecSet["objSpeed"]);

                //'Target base
                //Close previous reader
                RecSet.Close();
                query = "SELECT basID, basX, basY FROM tblbase WHERE basUserID = " + maUserID + " AND basHQ = 1";
                cmdRecSet.CommandText = query;
                RecSet = cmdRecSet.ExecuteReader();

                targetBaseID = Convert.ToInt64(RecSet["basID"]);
                x = Convert.ToInt64(RecSet["basX"]);
                y = Convert.ToInt64(RecSet["basY"]);

                //'Retrieve current base x,y
                base_x = getBaseXY(maTargetBaseID, "x");
                base_y = getBaseXY(maTargetBaseID, "y");

                //'Get target distance based on x,y
                distance = getDistance(base_x, base_y, x, y);

                //'Get ETA in seconds
                if (objSpeed == 0)
                {
                    //response.write "attid: " & id & " objID = " & objID
                    //Console.WriteLine("attid: " + id + " objID = " + objID);
                }
                eta = speedToTime(objSpeed, distance);

                //'Calculate arrival time based on slowest unit type
                arrivalTime = DateTime.Now.AddSeconds(eta);

                //'Insert into queue
                query_sendhome = query_sendhome + "INSERT INTO tblqueue(quUserID, quObjCount, quFinished, quObjBaseID, quObjID, quObjLoc, quType) VALUES('" + maUserID + "','" + objCount + "','" + arrivalTime.ToString("yyyy-MM-dd HH:mm:ss") + "','" + targetBaseID + "','" + objID + "','" + maTargetBaseID + "',1);";

                //'Remove from userobjects
                query_sendhome_del = query_sendhome_del + "DELETE FROM tbluobj WHERE uObjBaseID = " + maTargetBaseID + " AND uObjuID = " + maUserID + " AND uObjID = " + objID + ";";


            }
            //'---------------

            //'Stats

            string attacker_win = "staAttacksWon = staAttacksWon + 1, ";

            if (string.IsNullOrEmpty(Convert.ToString(losses_type[3, 2])))
            {
                losses_type[3, 2] = 0;
            }
            if (string.IsNullOrEmpty(Convert.ToString(losses_type[4, 2])))
            {
                losses_type[4, 2] = 0;
            }
            if (string.IsNullOrEmpty(Convert.ToString(losses_type[5, 2])))
            {
                losses_type[5, 2] = 0;
            }
            if (string.IsNullOrEmpty(Convert.ToString(losses_type[3, 1])))
            {
                losses_type[3, 1] = 0;
            }
            if (string.IsNullOrEmpty(Convert.ToString(losses_type[4, 1])))
            {
                losses_type[4, 1] = 0;
            }
            if (string.IsNullOrEmpty(Convert.ToString(losses_type[5, 1])))
            {
                losses_type[5, 1] = 0;
            }


            string query_attstat = "UPDATE tblstats SET staAttacks = staAttacks + 1, " + attacker_win + "staKills3 = staKills3 + " + losses_type[3, 2] + ", staKills4 = staKills4 + " + losses_type[4, 2] + ", staKills5 = staKills5 + " + losses_type[5, 2] + ", staLosses3 = staLosses3 + " + losses_type[3, 1] + ", staLosses4 = staLosses4 + " + losses_type[4, 1] + ", staLosses5 = staLosses5 + " + losses_type[5, 1] + " WHERE staUserID = " + maUserID;




            //'EXECUTE ALL QUERIES TO UPDATE DB
            //'------------------------------------------------
            string query_rep = "";
            string query_defrep = "";
            string query_attrep = "";
            string query_att = "";

            if (sim == 0)
            {
                TimeSpan ts = maArrTime - DateTime.Now;
                if (ts.Seconds <= 0) //'Only if time up
                {
                    if (cancel == false)
                    {

                        //'Send home
                        mExecute(ConnectToMySqlObj.myConn, query_sendhome);
                        mExecute(ConnectToMySqlObj.myConn, query_sendhome_del);

                        //'Save changes
                        mExecute(ConnectToMySqlObj.myConn, query_def1);
                        mExecute(ConnectToMySqlObj.myConn, query_def2);


                        query_att = "DELETE FROM tblmissileattack WHERE maID = " + maID;
                        cmdRecSetCommon.CommandText = query_att;
                        cmdRecSetCommon.ExecuteNonQuery();



                        //'Send report to attacker
                        query_attrep = "INSERT INTO tblmail(mDate,mSubject,mMessage,mToID,mRead,mFromID) VALUES('" + maArrTime.ToString("yyyy-MM-dd HH:mm:ss") + "','" + title.Replace("'", "''") + "','" + outprint.Replace("'", "''") + "'," + maUserID + ",0,-1)";
                        cmdRecSetCommon.CommandText = query_attrep;
                        cmdRecSetCommon.ExecuteNonQuery();

                        //'Send report to defender
                        query_defrep = "INSERT INTO tblmail(mDate,mSubject,mMessage,mToID,mRead,mFromID) VALUES('" + maArrTime.ToString("yyyy-MM-dd HH:mm:ss") + "','" + title.Replace("'", "''") + "','" + out_all.Replace("'", "''") + "'," + defID + ",0,-1)";
                        cmdRecSetCommon.CommandText = query_defrep;
                        cmdRecSetCommon.ExecuteNonQuery();

                        //'Save and archive report
                        // id is long thats why idString is used instead of id
                        string idString = maID + "-" + maBaseID + "-" + maUserID + "-" + maTargetBaseID + "-" + maType + "-" + maArrTime.ToString("yyyy-MM-dd HH:mm:ss").Replace(":", "").Replace(" ", "").Replace("-", "");

                        query_rep = "INSERT INTO tblattreport(attRepID, attReport,attRepTime,attRepAttacker,attRepDefender,attRepCode) VALUES('" + idString + "','" + out_all.Replace("'", "''") + "','" + maArrTime.ToString("yyyy-MM-dd HH:mm:ss") + "'," + maUserID + "," + defID + "," + code + ") ON DUPLICATE KEY UPDATE attReport = '" + out_all.Replace("'", "''") + "'";
                        cmdRecSetCommon.CommandText = query_rep;
                        cmdRecSetCommon.ExecuteNonQuery();


                        //'Save stats
                        cmdRecSetCommon.CommandText = query_attstat;
                        cmdRecSetCommon.ExecuteNonQuery();




                        //'Attack in  same alliance?
                        long allID = fUserAll(maUserID);
                        if (allID == fUserAll(defID))
                        {
                            //'Update mini-feed
                            query = "INSERT INTO tblallfeed(afAllID,afUserID,afEvent,afTime) VALUES(" + allID + "," + maUserID + ",5,'" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "')";
                            cmdRecSetCommon.CommandText = query;
                            cmdRecSetCommon.ExecuteNonQuery();
                        }


                    }
                }
            }
            else
            {
                //'Print (for debug)


                //Console.WriteLine(out_all);
                //Console.WriteLine("<br>" + query_def1 + "<br><br>" + query_def2 + "<br><br>".Replace(";",";<br>"));
                //Console.WriteLine(query_att + "<br>");
                //Console.WriteLine(query_attrep + "<br>");
                //Console.WriteLine(query_defrep + "<br>");
                //Console.WriteLine(query_rep + "<br>");
                //Console.WriteLine(query_sendhome + "<br>");
                //Console.WriteLine(query_sendhome_del + "<br>");
                //Console.WriteLine(query_attstat + "<br>");
                //Console.WriteLine(query_def1 + "<br>");
                //Console.WriteLine(query_def2 + "<br>");
                //Console.WriteLine("winner total: " + winner_total + "<br>");
                //Console.WriteLine(sim_msg);

            }



            //'--------------------------------------------------


            RecSetAtt.Close();
            RecSetAtt2.Close();

            RecSet.Close();
            RecSet2.Close();


        }

        //End of Missile Attack

        //Process Queue go Logging
        public void QueueGoLogging()
        {
            //'Log Vars
            DateTime logTime = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            decimal logNumBuildUnits = 0;
            decimal logNumBuildBuildings = 0;
            //decimal logNumMoveUnits = 0;
            //decimal logNumAttacks = 0;
            //decimal logNumSpies = 0;
            //decimal logNumRetSpies = 0;
            decimal logNumSpyUpg = 0;
            //decimal logNumBases = 0;
            //decimal logNumRetAttacks = 0;

            long logQueueCount = 0;
            //decimal logAttackCount = 0;
            //decimal logRetAttackCount = 0;
            //decimal logSpyCount = 0;
            long logQueueLoopCount = 0;
            //decimal logAttackLoopCount = 0;
            //decimal logRetAttackLoopCount = 0;
            decimal logDouble = 0;
            string logDoubleLog = "";
            decimal logDoubleMade = 0;

            //'Log numrows
            string query = "SELECT COUNT(*) AS cnt FROM tblQueue WHERE quFinished <= '" + logTime + "'";

            OdbcCommand cmdRecSet = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader RecSet = cmdRecSet.ExecuteReader();

            //Common Command for update, insert, delete
            OdbcCommand cmdRecSetCommon = new OdbcCommand("", ConnectToMySqlObj.myConn);


            if (RecSet.HasRows)
            {
                logQueueCount = Convert.ToInt64(RecSet["cnt"].ToString());
            }

            //First close previous odbc reader
            RecSet.Close();

            cmdRecSet.CommandText = "SELECT quID, quObjID, quUserID, quObjLoc, quObjBaseID, quObjCount, quType, quFinished FROM tblQueue WHERE quFinished <= '" + logTime + "' ORDER BY quFinished ASC";
            RecSet = cmdRecSet.ExecuteReader();

            if (RecSet.HasRows)
            {
                while (RecSet.Read())
                {

                    long quType = Convert.ToInt64(RecSet["quType"].ToString());
                    long quID = Convert.ToInt64(RecSet["quID"].ToString());
                    logQueueLoopCount = logQueueLoopCount + 1;

                    //'Processing log values
                    long procLogType = 0;
                    long userID = Convert.ToInt64(RecSet["quUserID"].ToString());
                    string baseID = RecSet["quObjBaseID"].ToString();
                    long quObjLoc = Convert.ToInt64(RecSet["quObjLoc"].ToString());
                    string procLogValue = procLogType + userID + baseID + quObjLoc + "&" + RecSet["quObjCount"].ToString() + "&" + RecSet["quObjID"].ToString() + RecSet["quFinished"].ToString().Replace(":", "").Replace(" ", "").Replace("-", "");
                    string procLogIdentifier = quID.ToString();


                    //'Unit movement or build object?
                    if (quType == 0) //'Build object
                    {
                        //'Getting info about if the user have the object since before

                        query = "SELECT uObjCount FROM tbluobj WHERE uObjID = " + RecSet["quObjID"] + " AND uObjuID = " + RecSet["quUserID"] + " AND uObjLoc = " + RecSet["quObjLoc"] + " AND uObjBaseID = " + RecSet["quObjBaseID"];

                        OdbcCommand cmdRecSet2 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                        OdbcDataReader RecSet2 = cmdRecSet2.ExecuteReader();

                        //'If Eof then insert, otherwise update
                        if (!RecSet2.HasRows)
                        {
                            //No Data
                            if (Convert.ToInt64(RecSet["quObjID"].ToString()) == Convert.ToInt64(cSpyID.ToString()))
                            {
                                //'If spy, check tblspy for existing spy
                                //'Get max level

                                query = "SELECT objMaxLevel FROM tblobjects WHERE objID = " + cSpyID;

                                OdbcCommand cmdRecSet3 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                                OdbcDataReader RecSet3 = cmdRecSet3.ExecuteReader();


                                int spyMaxLevel = int.Parse(RecSet3["objMaxLevel"].ToString());

                                //First close previous odbc reader
                                RecSet3.Close();

                                cmdRecSet3.CommandText = "SELECT spyLevel FROM tblspy WHERE spyUserID = " + RecSet["quUserID"] + " AND spyBaseID = " + RecSet["quObjBaseID"];
                                RecSet3 = cmdRecSet3.ExecuteReader();

                                if (RecSet3.HasRows) // equivalent to Not EOF
                                {
                                    if (!(int.Parse(RecSet3["spyLevel"].ToString()) > spyMaxLevel))
                                    {
                                        if (inProcLog(procLogValue) == false)
                                        {
                                            //'Update spy level
                                            cmdRecSetCommon.CommandText = "UPDATE tblspy SET spyLevel = spyLevel + 1 WHERE spyUserID = " + RecSet["quUserID"] + " AND spyBaseID = " + RecSet["quObjBaseID"];
                                            cmdRecSetCommon.ExecuteNonQuery();

                                            //'Save to processing log
                                            fProcLog(procLogType, userID, baseID, DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), procLogValue, procLogIdentifier);
                                            logNumSpyUpg = logNumSpyUpg + 1;

                                        }// End of if (inProcLog(procLogValue) == false)
                                        else
                                        {
                                            logDouble = logDouble + 1;
                                            logDoubleLog = logDoubleLog + procLogValue + "<br />";
                                        }// End of else (inProcLog(procLogValue) == false)

                                        //'Check if double was made
                                        if (fProcLogCount(procLogValue) > 1)
                                        {
                                            logDoubleMade = logDoubleMade + 1;
                                            logDoubleLog = logDoubleLog + procLogValue + " (WARNING: DOUBLE MADE, NOT IGNORED)<br />";
                                        }



                                    } // End of if (!(int.Parse(RecSet3["spyLevel"].ToString()) > spyMaxLevel))

                                }// End of if (RecSet3.HasRows)
                                else
                                {

                                    if (inProcLog(procLogValue) == false)
                                    {
                                        //'Insert new spy
                                        cmdRecSetCommon.CommandText = "Insert Into tbluobj(uObjuID, uObjID, uObjCount, uObjLoc, uObjBaseID, uObjLevel) Values(" + RecSet["quUserID"] + ", " + RecSet["quObjID"] + ", " + RecSet["quObjCount"] + ", " + RecSet["quObjLoc"] + ", " + RecSet["quObjBaseID"] + ", 1)";
                                        cmdRecSetCommon.ExecuteNonQuery();

                                        logNumBuildUnits = logNumBuildUnits + 1;

                                        //'Save to processing log
                                        fProcLog(procLogType, userID, baseID, DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), procLogValue, procLogIdentifier);

                                    }// End of if (inProcLog(procLogValue) == false)
                                    else
                                    {
                                        logDouble = logDouble + 1;
                                        logDoubleLog = logDoubleLog + procLogValue + "<br />";

                                    }// End of else (inProcLog(procLogValue) == false)

                                    //'Check if double was made
                                    if (fProcLogCount(procLogValue) > 1)
                                    {
                                        logDoubleMade = logDoubleMade + 1;
                                        logDoubleLog = logDoubleLog + procLogValue + " (WARNING: DOUBLE MADE, NOT IGNORED)<br />";
                                    }


                                } // End of else (RecSet3.HasRows)

                                //Close all used command and reader in the scope
                                RecSet3.Close();


                            }// End of if (Convert.ToInt64(RecSet["quObjID"].ToString()) == Convert.ToInt64(cSpyID.ToString()))
                            else
                            {

                                //'Adding post with the number of objects
                                if (inProcLog(procLogValue) == false)
                                {
                                    string outPut = RecSet["quUserID"].ToString() + " - " + RecSet["quObjID"].ToString() + " - " + RecSet["quObjBaseID"] + " - " + RecSet["quFinished"] + "<br>";
                                    //Console.WriteLine(outPut);
                                    cmdRecSetCommon.CommandText = "Insert Into tbluobj(uObjuID, uObjID, uObjCount, uObjLoc, uObjBaseID, uObjLevel) Values(" + RecSet["quUserID"] + ", " + RecSet["quObjID"] + ", " + RecSet["quObjCount"] + ", " + RecSet["quObjLoc"] + ", " + RecSet["quObjBaseID"] + ", 1) ON DUPLICATE KEY UPDATE uobjuid = uobjuid";
                                    cmdRecSetCommon.ExecuteNonQuery();

                                    if (Convert.ToInt64(RecSet["quObjLoc"].ToString()) == 99)
                                    {
                                        logNumBuildUnits = logNumBuildUnits + 1;
                                    }
                                    else
                                    {
                                        logNumBuildBuildings = logNumBuildBuildings + 1;
                                    }
                                    //'Save to processing log
                                    fProcLog(procLogType, userID, baseID, DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), procLogValue, procLogIdentifier);

                                } //End of if (inProcLog(procLogValue) == false)
                                else
                                {
                                    logDouble = logDouble + 1;
                                    logDoubleLog = logDoubleLog + procLogValue + " (Build units/building in base " + baseID + ") <br />";
                                }// End of else (inProcLog(procLogValue) == false)


                                //'Check if double was made
                                if (fProcLogCount(procLogValue) > 1)
                                {
                                    logDoubleMade = logDoubleMade + 1;
                                    logDoubleLog = logDoubleLog + procLogValue + " (WARNING: DOUBLE MADE, NOT IGNORED)<br />";
                                }


                            }// End of else (Convert.ToInt64(RecSet["quObjID"].ToString()) == Convert.ToInt64(cSpyID.ToString()))


                            cmdRecSetCommon.CommandText = "UPDATE tblbase SET basXP = basXP + (SELECT objXP FROM tblobjects WHERE objID = " + RecSet["quObjID"] + ") * " + RecSet["quObjCount"] + " WHERE basID = " + RecSet["quObjBaseID"];
                            cmdRecSetCommon.ExecuteNonQuery();


                        }// End of if (!RecSet2.HasRows)
                        else
                        {

                            //'Updating the post with the number of objects or level
                            if (Convert.ToInt64(RecSet["quObjID"].ToString()) == Convert.ToInt64(cSpyID.ToString()))
                            {

                                //'Update spy level
                                if (inProcLog(procLogValue) == false)
                                {
                                    cmdRecSetCommon.CommandText = "UPDATE tbluobj Set uObjLevel = uObjLevel + 1 WHERE " + RecSet["quUserID"] + " AND uObjID = " + RecSet["quObjID"] + " AND uObjBaseID = " + RecSet["quObjBaseID"];
                                    cmdRecSetCommon.ExecuteNonQuery();
                                    cmdRecSetCommon.CommandText = "UPDATE tblbase SET basXP = basXP + (SELECT objXP FROM tblobjects WHERE objID = " + RecSet["quObjID"] + ") WHERE basID = " + RecSet["quObjBaseID"];
                                    cmdRecSetCommon.ExecuteNonQuery();
                                    logNumSpyUpg = logNumSpyUpg + 1;
                                    //'Save to processing log
                                    fProcLog(procLogType, userID, baseID, DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), procLogValue, procLogIdentifier);

                                }//End of if(inProcLog(procLogValue) == false)
                                else
                                {
                                    logDouble = logDouble + 1;
                                    logDoubleLog = logDoubleLog + procLogValue + " (Spy upgrade) <br />";
                                }//End of else(inProcLog(procLogValue) == false)


                                //'Check if double was made
                                if (fProcLogCount(procLogValue) > 1)
                                {
                                    logDoubleMade = logDoubleMade + 1;
                                    logDoubleLog = logDoubleLog + procLogValue + " (WARNING: DOUBLE MADE, NOT IGNORED)<br />";
                                }


                            }//End of if (Convert.ToInt64(RecSet["quObjID"].ToString()) == Convert.ToInt64(cSpyID.ToString())
                            else
                            {

                            }//End of else (Convert.ToInt64(RecSet["quObjID"].ToString()) == Convert.ToInt64(cSpyID.ToString())




                        }// End of else (!RecSet2.HasRows)



                    }//End of if(quType = 0)


                }// End of while (RecSet.Read())
            } // End of if (RecSet.HasRows)

        }
        //End of Process Queue go Logging





        //Functions from connect.asp

        //'Returns defense plan in text format from base id
        public string defPlan(long baseID)
        {
            string query = "SELECT basDefDist, basDefAggr, basDefMob FROM tblbase WHERE basID = " + baseID;
            OdbcCommand cmddefPlan = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader ReaderdefPlan = cmddefPlan.ExecuteReader();

            double dist = 0;
            double aggr = 0;
            double mob = 0;

            if (ReaderdefPlan.HasRows)
            {
                dist = Convert.ToDouble(ReaderdefPlan["basDefDist"].ToString());
                aggr = Convert.ToDouble(ReaderdefPlan["basDefAggr"].ToString());
                mob = Convert.ToDouble(ReaderdefPlan["basDefMob"].ToString());
            }

            string returnValue = "";
            if (ReaderdefPlan.HasRows)
            {
                //Close previous reader
                ReaderdefPlan.Close();
                query = "SELECT planName FROM tblplans WHERE planDist = '" + dist + "' AND planAggr = '" + aggr + "' AND planMob = '" + mob + "'";
                cmddefPlan.CommandText = query;
                ReaderdefPlan = cmddefPlan.ExecuteReader();
                if (!ReaderdefPlan.HasRows)
                {
                    returnValue = returnValue + text[548] + " " + dist * 100 + "%<br />";
                    returnValue = returnValue + text[549] + " " + aggr * 100 + "%<br />";
                    returnValue = returnValue + text[550] + " " + mob * 100 + "%<br />";
                }
                else
                {
                    returnValue = ReaderdefPlan["planName"].ToString();
                }
            }

            return returnValue;
        }


        public bool spyLvlVsBase(long level, long baseValue)
        {
            bool returnValue = false;
            level = 55333 * level - 53333;
            //'Random outcome by 20%
            Random Rnd = new Random();
            double rand = ((1.3 - 0.6) * Rnd.NextDouble() + 0.6);
            double levelFinal = level * rand;
            if (levelFinal >= baseValue)
            {
                returnValue = true;
            }
            else
            {
                returnValue = false;
            }

            return returnValue;
        }


        //'Saves a transaction
        public void fTransLog(long userID, long baseID, long objID, long amount)
        {

            //'Update player money and resources
            decimal baseHourCost = bas_cost(userID, baseID);
            long resStoreLevel = resStoreLvl(userID, baseID);
            long basRes = bas_res(userID, baseID, Math.Round((Convert.ToDecimal(resInc(userID, baseID))) / 60, 3), baseHourCost, resSpace(resStoreLevel));

            //'Retrieve players current balance
            string query = "SELECT basMoney FROM tblbase WHERE basID = " + baseID;
            OdbcCommand cmdfTransLog = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader ReaderfTransLog = cmdfTransLog.ExecuteReader();

            long balance = 0;
            if (ReaderfTransLog.HasRows)
            {
                balance = Convert.ToInt64(ReaderfTransLog["basMoney"]);
            }
            else
            {
                balance = 0;
            }

            if (string.IsNullOrEmpty(Convert.ToString(amount)))
            {
                amount = 0;
            }

            //'Insert
            query = "INSERT INTO tbltransactions(traUserID, traBaseID, traObjID, traMoney, traDate, traBalance) VALUES(" + userID + "," + baseID + "," + objID + "," + amount + ",'" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "','" + (balance + amount) + "')";
            cmdRecSetCommon.CommandText = query;
            cmdRecSetCommon.ExecuteNonQuery();

            ReaderfTransLog.Close();

        }


        //'Alliance id from user ID
        public long fUserAll(long id)
        {
            long returnValue = 0;
            if (string.IsNullOrEmpty(Convert.ToString(id)))
            {
                returnValue = 0;
            }
            else
            {
                string query = "SELECT uAllID FROM tbluser WHERE uID = '" + id + "'";
                OdbcCommand cmdfUserAll = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                OdbcDataReader ReaderfUserAll = cmdfUserAll.ExecuteReader();

                if (ReaderfUserAll.HasRows)
                {
                    if (Convert.ToString(ReaderfUserAll["uAllID"]).Length > 0)
                    {
                        returnValue = Convert.ToInt64(ReaderfUserAll["uAllID"]);
                    }
                    else
                    {
                        returnValue = 0;
                    }
                }

                ReaderfUserAll.Close();
            }

            return returnValue;
        }

        //'Determine if an attack already has been performed
        public bool attExists(string attGroupID)
        {
            bool returnValue = false;
            string query = "SELECT (1) FROM tblattreport WHERE attRepID = '" + attGroupID + "'";
            OdbcCommand cmdattExists = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader ReaderattExists = cmdattExists.ExecuteReader();
            if (!ReaderattExists.HasRows)
            {
                returnValue = false;
            }
            else
            {
                returnValue = true;
            }

            return returnValue;
        }

        //'Retrieve money count for a base
        public long fBaseMoney(long baseID)
        {
            string query = "SELECT basMoney FROM tblbase WHERE basID = '" + baseID + "'";
            OdbcCommand cmdfBaseMoney = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader ReaderfBaseMoney = cmdfBaseMoney.ExecuteReader();

            long returnValue = 0;

            if (!ReaderfBaseMoney.HasRows)
            {
                returnValue = 0;
            }
            else
            {
                returnValue = Convert.ToInt64(ReaderfBaseMoney["basMoney"]);
            }
            return returnValue;
        }

        //'Determine if an object is a building
        public long isBuilding(long id)
        {
            long returnValue = 0;
            if (id == 1 || id == 2)
            {
                id = 12;
            }
            returnValue = id;
            return returnValue;
        }

        //'Calculate number of survivors after an attack
        public decimal calc_winner_survivors(decimal val1, decimal val2)
        {
            decimal returnValue = 0;
            if (val1 > val2)
            {
                if (val1 == 0)
                {
                    returnValue = 0;
                }
                else
                {
                    returnValue = Math.Round(((val1 - val2) / val1), 3);
                }
            }
            else if (val2 > val1)
            {
                if (val2 == 0)
                {
                    returnValue = 0;
                }
                else
                {
                    returnValue = Math.Round(((val2 - val1) / val2), 3);
                }
            }
            else
            {
                returnValue = 0;
            }

            return returnValue;
        }


        //'Determine winner based on two values
        public string calc_winner(decimal attacker, decimal defender, string outtype)
        {
            string returnValue = "0";
            if (attacker > defender)
            {
                if (outtype == "name")
                {
                    returnValue = "Attacker";
                }
                else
                {
                    returnValue = "1";
                }
            }
            else if (defender > attacker)
            {
                if (outtype == "name")
                {
                    returnValue = "Defender";
                }
                else
                {
                    returnValue = "2";
                }
            }
            else
            {
                if (outtype == "name")
                {
                    returnValue = "Tie! Nobody";
                }
                else
                {
                    returnValue = "0";
                }
            }

            return returnValue;
        }

        //'Returns which player gets the bonus based on parameter type and players parameter values
        public long fParamUserBonus(string param, double attacker, double defender)
        {
            long returnValue = 0;
            long[,] bonusTo = new long[2 + 1, 2 + 1];
            //'-1 -> 2 to avoid negative array index

            switch (param)
            {
                case "dist":
                    bonusTo[2, 2] = 0;
                    bonusTo[2, 0] = 1;
                    bonusTo[2, 1] = 1;

                    bonusTo[0, 2] = 1;
                    bonusTo[0, 0] = 0;
                    bonusTo[0, 1] = 1;

                    bonusTo[1, 2] = 1;
                    bonusTo[1, 0] = 1;
                    bonusTo[1, 1] = 0;
                    break;
                case "aggr":
                    bonusTo[2, 2] = 0;
                    bonusTo[2, 0] = 1;
                    bonusTo[2, 1] = 0;

                    bonusTo[0, 2] = 2;
                    bonusTo[0, 0] = 0;
                    bonusTo[0, 1] = 2;

                    bonusTo[1, 2] = 0;
                    bonusTo[1, 0] = 1;
                    bonusTo[1, 1] = 0;
                    break;
                case "mob":
                    bonusTo[2, 2] = 0;
                    bonusTo[2, 0] = 1;
                    bonusTo[2, 1] = 2;

                    bonusTo[0, 2] = 2;
                    bonusTo[0, 0] = 0;
                    bonusTo[0, 1] = 0;

                    bonusTo[1, 2] = 1;
                    bonusTo[1, 0] = 0;
                    bonusTo[1, 1] = 0;
                    break;
            }

            long rAtt = Convert.ToInt64(Math.Round(attacker, 0));
            long rDef = Convert.ToInt64(Math.Round(defender, 0));

            if (rAtt == -1)
            {
                rAtt = 2;
            }
            if (rDef == -1)
            {
                rDef = 2;
            }

            returnValue = bonusTo[rAtt, rDef];

            return returnValue;
        }

        //'Returns base defense bonus for a specific base
        public long basDefBonus(long userid, long baseid)
        {
            string query = "SELECT uObjLevel FROM tbluobj WHERE uObjID = " + cGuardTowerID + " AND uObjuID = " + userid + " AND uObjBaseID = " + baseid;
            OdbcCommand cmdbasDefBonus = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader ReaderbasDefBonus = cmdbasDefBonus.ExecuteReader();

            long returnValue = 0;

            while (ReaderbasDefBonus.Read())
            {
                returnValue = returnValue + Convert.ToInt64(ReaderbasDefBonus["uObjLevel"]) * cTowerBasDefBonus;
            }
            //Close previous reader
            ReaderbasDefBonus.Close();
            query = "SELECT uObjLevel FROM tbluobj WHERE uObjID = " + cFenceID + " AND uObjuID = " + userid + " AND uObjBaseID = " + baseid;
            cmdbasDefBonus.CommandText = query;
            ReaderbasDefBonus = cmdbasDefBonus.ExecuteReader();
            while (ReaderbasDefBonus.Read())
            {
                returnValue = returnValue + Convert.ToInt64(ReaderbasDefBonus["uObjLevel"]) * cFenceBasDefBonus;
            }

            ReaderbasDefBonus.Close();
            return returnValue;
        }


        //'Returns total amount of protected money for a user
        public long valveFromBase(long id)
        {
            long returnValue = 0;
            long valve = 0;
            if (id <= 0)
            {
                returnValue = 0;
            }
            else
            {
                string query = "SELECT uObjLevel FROM tbluobj WHERE uObjID = " + cValveID + " AND uObjBaseID = " + id;
                OdbcCommand cmdvalveFromBase = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                OdbcDataReader ReadervalveFromBase = cmdvalveFromBase.ExecuteReader();
                if (!ReadervalveFromBase.HasRows)
                {
                    returnValue = 0;
                }
                else
                {
                    valve = 0;
                    while (ReadervalveFromBase.Read())
                    {
                        long level = Convert.ToInt64(ReadervalveFromBase["uObjLevel"]);
                        valve = valve + valveFromLevel(level);
                    }
                }
                returnValue = valve;

                ReadervalveFromBase.Close();
            }


            return returnValue;

        }

        //'Returns how much money is protected in a bank valve based on level
        public long valveFromLevel(long lvl)
        {
            long returnValue = 0;
            if (lvl <= 0)
            {
                returnValue = 0;
            }
            else
            {
                returnValue = RoundToValue(((lvl * 252.63 - 52.63)), 10, true);
            }
            return returnValue;
        }

        //'Calculate resources storage space. level=current level. /henrik
        public long resSpace(long level)
        {
            long returnValue = 0;
            double x = 10692 * cE;
            double y = 0.1476 * level;
            if (level == 0)
            {
                returnValue = cStoreSpaceLvl0;
            }
            else
            {
                returnValue = RoundToValue(Math.Pow(x, y), 10, true);
            }

            return returnValue;
        }

        //'Rounds up/down to specified value
        //'Source: Chris Hanscom/FreeVBcode.com. Converted to ASP by Henrik Tibbing
        public long RoundToValue(double nValue, long nCeiling, bool RoundUp)
        {
            RoundUp = true;
            long tmp = 0;
            double tmpVal = 0;

            //no need to check
            //if(! IsNumeric(nValue)
            //{
            //}
            //nValue = CDbl(nValue)

            //'Round up to a whole integer -
            //'Any decimal value will force a round to the next integer.
            //'i.e. 0.01 = 1 or 0.8 = 1

            tmpVal = ((nValue / nCeiling) + (-0.5 + Convert.ToInt64(RoundUp && true)));
            tmp = Convert.ToInt64(Math.Truncate(tmpVal));
            tmpVal = tmpVal - tmp;
            nValue = tmp + tmpVal;

            //'Multiply by ceiling value to set RoundtoValue
            long returnValue = Convert.ToInt64(nValue) * nCeiling;

            return returnValue;

        }

        //'Calculate troops cost and update money for a base. id=user id; baseid=base id/henrik
        public decimal bas_cost(long id, long baseid)
        {
            decimal returnValue = 0;

            if (baseid == 0 || string.IsNullOrEmpty(Convert.ToString(baseid)))
            {
                returnValue = 0;
            }
            else
            {
                //'Troops in base
                string query = "SELECT tbluobj.uObjCount, tblobjects.obj24HourCost FROM tbluobj " + "INNER JOIN tblobjects ON tbluobj.uObjID = tblobjects.objID WHERE tbluobj.uObjBaseID = " + baseid + " AND tbluobj.uObjLoc = 99 ORDER BY objName";

                OdbcCommand cmdbas_cost = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                OdbcDataReader Readerbas_cost = cmdbas_cost.ExecuteReader();

                decimal totalcost = 0;
                long objCount = 0;
                decimal obj24HourCost = 0;
                while (Readerbas_cost.Read())
                {
                    objCount = Convert.ToInt64(Readerbas_cost["uObjCount"]);
                    obj24HourCost = decimal.Round(Convert.ToDecimal(Readerbas_cost["obj24HourCost"]), 2);
                    totalcost = totalcost + objCount * (obj24HourCost / 24);
                }

                //first close previous reader
                Readerbas_cost.Close();

                //'Troops in movement
                query = "SELECT tblqueue.quObjCount, tblobjects.obj24HourCost FROM tblqueue " + "INNER JOIN tblobjects ON tblqueue.quObjID = tblobjects.objID WHERE tblqueue.quObjLoc = " + baseid + " AND tblqueue.quType = 1 ORDER BY objName";

                cmdbas_cost.CommandText = query;

                Readerbas_cost = cmdbas_cost.ExecuteReader();

                while (Readerbas_cost.Read())
                {
                    objCount = Convert.ToInt64(Readerbas_cost["quObjCount"]);
                    obj24HourCost = decimal.Round(Convert.ToDecimal(Readerbas_cost["obj24HourCost"]), 2);
                    totalcost = totalcost + objCount * (obj24HourCost / 24);
                }
                returnValue = decimal.Round(totalcost, 1);

                Readerbas_cost.Close();
            }

            return returnValue;

        }


        //'Calculate increase of resources. /henrik
        public double resInc(long uID, long baseID)
        {
            string query = "SELECT basType FROM tblbase WHERE basUserID = " + uID + " AND basID = " + baseID;
            OdbcCommand cmdresInc = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader ReaderresInc = cmdresInc.ExecuteReader();
            long basType;
            if (ReaderresInc.HasRows)
            {
                basType = Convert.ToInt64(ReaderresInc["basType"]);
            }
            else
            {
                basType = 1;
            }

            long resGainID;
            if (basType == 1)
            {
                resGainID = 12;
            }
            else if (basType == 2)
            {
                resGainID = 13;
            }
            else
            {
                resGainID = 12;
            }

            //first close previous reader
            ReaderresInc.Close();

            query = "SELECT uObjLevel FROM tbluobj WHERE uObjID = " + resGainID + " AND uObjuID = " + uID + " AND uObjBaseID = " + baseID;
            cmdresInc.CommandText = query;

            ReaderresInc = cmdresInc.ExecuteReader();

            double returnValue;

            if (!ReaderresInc.HasRows)//if EOF
            {
                returnValue = cLvl0Increase;
            }
            else
            {
                returnValue = 0;
                long level;
                while (ReaderresInc.Read())
                {
                    level = Convert.ToInt64(ReaderresInc["uObjLevel"]);
                    //'resInc = resInc + (28.94 * cE^(0.1197*level))
                    //'resInc = resInc + 0.6276 * level^2 + 0.6879*level + 35.202
                    returnValue = returnValue + 8.0398 * (level * level) + (68.007 * level) + 423.95;
                }
            }

            //'Check for production advantage
            if (fUserAdv(1, uID, 0) != DateTime.MinValue)
            {
                returnValue = Math.Round(returnValue * (1 + cCredProdInc), 0);
            }

            ReaderresInc.Close();

            return returnValue;
        }


        // 'User id from baseID
        public long fBaseToUser(long id)
        {
            string query = "SELECT basUserID FROM tblbase WHERE basID = " + id;
            OdbcCommand cmdfBaseToUser = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader ReaderfBaseToUser = cmdfBaseToUser.ExecuteReader();
            long returnId;
            if (ReaderfBaseToUser.HasRows)
            {
                returnId = Convert.ToInt64(ReaderfBaseToUser["basUserID"]);
            }
            else
            {
                returnId = 0;
            }

            ReaderfBaseToUser.Close();

            return returnId;
        }


        //'Number of units in tblobjects (option to include buildings)
        public long fNumObjects(string incBuildings)
        {
            string query = "";
            if (incBuildings == "True")
            {
                query = "SELECT COUNT(*) AS cnt FROM tblObjects";
            }
            else
            {
                query = "SELECT COUNT(*) AS cnt FROM tblobjects WHERE objCat > 2";
            }
            OdbcCommand cmdfNumObjects = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader ReaderfNumObjects = cmdfNumObjects.ExecuteReader();
            long count;
            if (ReaderfNumObjects.HasRows)
            {
                count = Convert.ToInt64(ReaderfNumObjects["cnt"]);
            }
            else
            {
                count = 0;
            }

            ReaderfNumObjects.Close();

            return count;
        }



        //'Determine count of a value in processing log
        public long fProcLogCount(string procLogValue)
        {
            string query = "SELECT COUNT(*) AS logCount FROM tblproclog WHERE procLogValue = '" + procLogValue + "'";
            OdbcCommand cmdfProcLogCount = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader ReaderfProcLogCount = cmdfProcLogCount.ExecuteReader();

            long count;

            if (!ReaderfProcLogCount.HasRows)
            {
                count = 0;
            }
            else
            {
                count = Convert.ToInt64(ReaderfProcLogCount["logCount"].ToString());
            }

            ReaderfProcLogCount.Close();

            return count;

        }

        //'Save into processing log
        public void fProcLog(long procLogType, long userID, string baseID, DateTime procLogTime, string procLogValue, string procLogIdentifier)
        {
            string query = "INSERT INTO tblproclog(procLogType,procLogUserID,procLogBaseID,procLogTime,procLogValue,procLogIdentifier) VALUES(" + procLogType + "," + userID + ",'" + baseID + "','" + procLogTime.ToString("yyyy-MM-dd HH:mm:ss") + "','" + procLogValue + "','" + procLogIdentifier + "')";

            OdbcCommand cmdfProcLog = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            cmdfProcLog.ExecuteNonQuery();
        }

        //'Determine if value exists in processing log
        public bool inProcLog(string procLogValue)
        {
            string query = "SELECT (1) FROM tblproclog WHERE procLogValue = '" + procLogValue + "'";

            OdbcCommand cmdinProcLog = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader ReaderinProcLog = cmdinProcLog.ExecuteReader();

            bool returnType;

            if (!ReaderinProcLog.HasRows)
            {
                returnType = false;
            }
            else
            {
                returnType = true;
            }

            ReaderinProcLog.Close();

            return returnType;

        }


        //'Returns x or y value from (x,y)
        public string getXY(string xy, string xytype)
        {

            if (xy.IndexOf(",") == 0)
            {
                return "0";
            }
            else
            {
                string splitter = ",";
                string[] arrxy = xy.Split(splitter.ToCharArray(0, 1));
                if (xytype == "y")
                {
                    return arrxy[1];
                }
                else
                {
                    return arrxy[0];
                }
            }
        }

        //'Returns base type from x,y
        public int baseType(int x, int y)
        {
            //Default return value 1
            int returnValue = 1;
            if (x < 0 && y < 0)
                returnValue = 3;
            if (x < 0 && y > 0)
                returnValue = 1;
            if (x > 0 && y < 0)
                returnValue = 4;
            if (x > 0 && y > 0)
                returnValue = 2;
            if (x == 0 && y < 0)
                returnValue = 3;
            if (x == 0 && y > 0)
                returnValue = 2;
            if (x > 0 && y == 0)
                returnValue = 4;
            if (x < 0 && y == 0)
                returnValue = 1;

            return returnValue;
        }


        //'Calculate and update resources and money for a base. id=user id; baseid=base id; inc=increment(res/min); dec=decrease(mon/h); max=upper limit/henrik
        public long bas_res(long id, long baseid, decimal inc, decimal dec, long max)
        {
            long returnValue = 0;
            if (id > 0)
            {
                string query = "SELECT basResources, basLastUpd, basMoney FROM tblbase WHERE basID = " + baseid + " AND basUserID = " + id;

                OdbcCommand cmdRs2 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                OdbcDataReader Rs2 = cmdRs2.ExecuteReader();



                long basMoney = 0;
                long basResNew = 0;
                long basMoneyNew = 0;

                if (Rs2.HasRows) //Equivalent to not EOF
                {
                    long basRes = Convert.ToInt64(Rs2["basResources"].ToString());
                    DateTime basLastUpd;
                    if (Convert.ToDateTime(Rs2["basLastUpd"]) == DateTime.MinValue) // That means null
                    {
                        basLastUpd = DateTime.Now;
                    }
                    else
                    {
                        basLastUpd = Convert.ToDateTime(Rs2["basLastUpd"].ToString());
                    }

                    basMoney = Convert.ToInt64(Rs2["basMoney"].ToString());

                    //'Check for production advantage
                    if (fUserAdv(1, id, 0) != DateTime.MinValue)
                    {
                        inc = Math.Round(inc * (1 + Convert.ToDecimal(cCredProdInc)), 0);
                    }

                    //'Calc resources
                    TimeSpan ts = basLastUpd - DateTime.Now;
                    basResNew = Convert.ToInt64(basRes + decimal.Round(inc * (ts.Seconds) / 60, 0));
                    if (basResNew > max)
                    {
                        basResNew = max;
                    }

                    //'Calc money
                    basMoneyNew = basMoney - Convert.ToInt64(decimal.Round(dec * (ts.Seconds) / 60 / 60, 0));

                    //'Check if last updated time is in the future
                    if (basMoneyNew > basMoney)
                    {
                        basMoneyNew = basMoney;
                    }
                    if (basResNew < basRes)
                    {
                        basResNew = basRes;
                    }

                    long basMoneyNewAbs = 0;

                    if (basMoneyNew <= 0)//'Out of money - delete troops in base and send pm
                    {
                        basMoneyNewAbs = basMoneyNew;
                        basMoneyNew = 0;



                        //'Out of money - delete user's troops

                        //First close previous odbc reader
                        Rs2.Close();
                        query = "SELECT uObjID, uObjCount FROM tbluobj WHERE uObjuID = " + id + " AND uObjBaseID = " + baseid + " AND uObjLoc = 99";
                        cmdRs2.CommandText = query;
                        Rs2 = cmdRs2.ExecuteReader();

                        if (Rs2.HasRows) //Equivalent to not EOF
                        {

                            //'Insert into log
                            string logoutput = "User: (" + id + ") " + fUserName(id) + "<br>";
                            logoutput = logoutput + "Base: (" + baseid + ") " + fBaseName(baseid) + "<br>";
                            logoutput = logoutput + "Current time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "<br>";
                            logoutput = logoutput + "Current money: $" + basMoney + "<br>";
                            logoutput = logoutput + "Last updated: " + basLastUpd.ToString("yyyy-MM-dd HH:mm:ss") + "<br>";
                            logoutput = logoutput + "Decrease: " + dec + " $/h<br>";
                            logoutput = logoutput + "New money: $" + basMoney + " - $" + decimal.Round(dec * (ts.Seconds) / 60 / 60, 0) + " = $" + basMoneyNewAbs + "<br>";

                            logoutput = logoutput + "<br><br>The following units have abandoned the base: <br />";

                            while (Rs2.Read())
                            {
                                logoutput = logoutput + "(" + Rs2["uObjCount"] + ") ID: " + Rs2["uObjID"] + "<br>";
                            }

                            //'Calculate time when units leave
                            DateTime leaveTime;
                            if (dec == 0)
                            {
                                leaveTime = DateTime.Now;
                            }
                            else
                            {
                                leaveTime = DateTime.Now.AddHours(Convert.ToDouble(-1 * (Math.Abs((basMoneyNewAbs)) / dec)));
                            }

                            cmdRecSetCommon.CommandText = "INSERT INTO tbllog(loginfo,logoutput,logtime,loguserid,logbaseid) VALUES('Out of money', '" + logoutput + "','" + leaveTime.ToString("yyyy-MM-dd HH:mm:ss") + "'," + id + "," + baseid + ")";
                            cmdRecSetCommon.ExecuteNonQuery();

                            cmdRecSetCommon.CommandText = "INSERT INTO tblmail(mDate,mSubject,mMessage,mToID,mRead,mFromID) VALUES('" + leaveTime.ToString("yyyy-MM-dd HH:mm:ss") + "','" + text[276] + "','" + text[277] + "<br />- " + fBaseName(baseid) + "'," + id + ",0,-1)";
                            cmdRecSetCommon.ExecuteNonQuery();

                            cmdRecSetCommon.CommandText = "DELETE FROM tbluobj WHERE uObjuID = " + id + " AND uObjBaseID = " + baseid + " AND uObjLoc = 99";
                            cmdRecSetCommon.ExecuteNonQuery();


                        }//End of if (Rs2.HasRows)

                        //'Out of money - send home reinforcing troops
                        query = "SELECT uObjCount, uObjID, uObjuID FROM tbluobj WHERE uObjuID <> " + id + " AND uObjBaseID = " + baseid + " AND uObjLoc = 99";

                        OdbcCommand cmdRs3 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
                        OdbcDataReader Rs3 = cmdRs3.ExecuteReader();

                        if (Rs3.HasRows) //Equivalent to not EOF
                        {
                            cmdRecSetCommon.CommandText = "INSERT INTO tblmail(mDate,mSubject,mMessage,mToID,mRead,mFromID) VALUES('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "','" + text[489] + "','" + text[490] + "<br />- " + fBaseName(baseid) + "'," + id + ",0,-1)";
                            cmdRecSetCommon.ExecuteNonQuery();

                            //'Count

                            string query_sendhome = "";
                            string query_sendhome_del = "";

                            while (Rs3.Read())
                            {

                                long objCount = Convert.ToInt64(Rs3["uObjCount"]);
                                long objID = Convert.ToInt64(Rs3["uObjID"]);
                                long uObjuID = Convert.ToInt64(Rs3["uObjuID"]);

                                //'Speed
                                //First close previous odbc reader
                                Rs2.Close();
                                query = "SELECT objSpeed FROM tblobjects WHERE objID = " + objID;
                                cmdRs2.CommandText = query;
                                Rs2 = cmdRs2.ExecuteReader();
                                long objSpeed = 0;
                                if (Rs2.HasRows)
                                {
                                    objSpeed = Convert.ToInt64(Rs2["objSpeed"]);
                                }

                                //'Target base
                                //First close previous odbc reader
                                Rs2.Close();
                                query = "SELECT basID, basX, basY FROM tblbase WHERE basUserID = " + uObjuID + " AND basHQ = 1";
                                cmdRs2.CommandText = query;
                                Rs2 = cmdRs2.ExecuteReader();
                                long targetBaseID = 0;
                                long x = 0;
                                long y = 0;
                                if (Rs2.HasRows)
                                {
                                    targetBaseID = Convert.ToInt64(Rs2["basID"]);
                                    x = Convert.ToInt64(Rs2["basX"]);
                                    y = Convert.ToInt64(Rs2["basY"]);
                                }

                                //'Retrieve current base x,y
                                long base_x = getBaseXY(baseid, "x");
                                long base_y = getBaseXY(baseid, "y");

                                //'Get target distance based on x,y
                                double distance = getDistance(base_x, base_y, x, y);

                                //'Get ETA in seconds
                                double eta = speedToTime(objSpeed, distance);

                                //'Check for speed advantage
                                if (fUserAdv(2, id, 0) != DateTime.MinValue)
                                {
                                    decimal temp = Convert.ToDecimal(eta);
                                    eta = Convert.ToDouble(decimal.Round(temp * (1 - cCredSpeedInc), 0));
                                }

                                //'Calculate arrival time based on slowest unit type
                                DateTime arrivalTime = DateTime.Now.AddSeconds(eta);

                                //'Insert into queue
                                query_sendhome = query_sendhome + "INSERT INTO tblqueue(quUserID, quObjCount, quFinished, quObjBaseID, quObjID, quObjLoc, quType) VALUES('" + uObjuID + "','" + objCount + "','" + arrivalTime.ToString("yyyy-MM-dd dd:mm:ss") + "','" + targetBaseID + "','" + objID + "','" + baseid + "',1);";

                                //'Remove from userobjects
                                query_sendhome_del = query_sendhome_del + "DELETE FROM tbluobj WHERE uObjBaseID = " + baseid + " AND uObjuID = " + uObjuID + " AND uObjID = " + objID + ";";


                            }//End of while (Rs3.Read())

                            //'Send home
                            mExecute(ConnectToMySqlObj.myConn, query_sendhome);
                            mExecute(ConnectToMySqlObj.myConn, query_sendhome_del);


                        }//End of if (Rs3.HasRows)

                        //Close ODBC Reader
                        Rs3.Close();


                    }//End of if (basMoneyNew <= 0)



                    if (basMoney.ToString().Length == 0)
                    {
                        basMoney = 0;
                    }

                    query = "UPDATE tblbase SET basResources = " + basResNew + ", basMoney = " + basMoneyNew + ", basLastUpd = '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "' WHERE basID = " + baseid + " AND basUserID = " + id;
                    if (basResNew.ToString().Length > 0 && basMoneyNew.ToString().Length > 0 && baseid.ToString().Length > 0)
                    {
                        cmdRecSetCommon.CommandText = query;
                        cmdRecSetCommon.ExecuteNonQuery();
                    }
                    else
                    {
                        //Console.WriteLine("query failed: " + query);
                    }

                    returnValue = basResNew;



                }//End of if (Rs2.HasRows)
                else
                {
                    returnValue = 0;
                }

            }
            else
            {
                returnValue = 0;
            }

            return returnValue;


        }



        //'Checks if user has a certain advantage (returns to date) base=0 -> all bases
        public DateTime fUserAdv(long adv, long user, long baseid)
        {
            string query = "SELECT advtodate FROM tbladvantage WHERE advType = " + adv + " AND advUserID = " + user + " AND advBaseID = " + baseid + " AND advToDate >= '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "'";
            OdbcCommand cmdfUserAdv = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader ReaderfUserAdv = cmdfUserAdv.ExecuteReader();
            DateTime returnValue;
            if (ReaderfUserAdv.HasRows)
            {
                returnValue = Convert.ToDateTime(ReaderfUserAdv["advtodate"]);
            }
            else
            {
                returnValue = DateTime.MinValue;
            }

            return returnValue;
        }


        //'Converts in-time to local time
        public DateTime fLocalTime(DateTime inTime, double local)
        {
            DateTime returnValue;
            returnValue = inTime.AddHours(local);
            return returnValue;
        }


        //'--Execute multiple queries--
        //'Separate the queries with a semicolon ;
        //'connection = connection object, typically 'connect' or 'conn'
        //'(C) Henrik Tibbing 2008
        public void mExecute(OdbcConnection connection, string query)
        {
            long i;
            string splitter = ";";
            string[] arrQuery = query.Split(splitter.ToCharArray(0, 1));
            for (i = 0; i < arrQuery.Length; i++)
            {
                if (!(arrQuery[i] == ""))
                {

                    cmdRecSetCommon.CommandText = arrQuery[i];
                    cmdRecSetCommon.ExecuteNonQuery();
                }
            }
        }



        //'Returns x and y coordinates for a base id. xytype="x" returns x-value, xytype="y" returns y-value, xytype=NULL returns (x,y)/henrik
        public long getBaseXY(long id, string xytype)
        {
            string query = "SELECT basX, basY FROM tblbase WHERE basID = " + id;
            OdbcCommand cmdgetBaseXY = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader ReadergetBaseXY = cmdgetBaseXY.ExecuteReader();
            long returnValue;
            if (!ReadergetBaseXY.HasRows)
            {
                returnValue = 0;
            }
            else
            {
                if (xytype == "x")
                {
                    returnValue = Convert.ToInt64(ReadergetBaseXY["basX"]);
                }
                else if (xytype == "y")
                {
                    returnValue = Convert.ToInt64(ReadergetBaseXY["basY"]);
                }
                else
                {
                    returnValue = Convert.ToInt64(ReadergetBaseXY["basX"] + "," + ReadergetBaseXY["basY"]);
                }

            }

            ReadergetBaseXY.Close();

            return returnValue;
        }

        //'Calculates and returns distance (in number of squares) between two points in the map/henrik
        public double getDistance(long x1, long y1, long x2, long y2)
        {
            //'Pythagoras sats ger: distance=sqrt(dx^2+dy^2)
            double returnValue;
            long dx = x2 - x1;
            long dy = y2 - y1;
            returnValue = Math.Sqrt(dx * dx + dy * dy);
            return returnValue;
        }


        //'Returns time in seconds for a unit to travel specified distance at specified speed
        public double speedToTime(long speed, double distance)
        {
            //'Utgår från: Mercenary, speed 8 tar 120 sekunder att gå en ruta -> cSpeedConst=120*8
            double returnValue = 0;
            if (speed == 0)
            {
                returnValue = 0;
            }
            else
            {
                returnValue = distance / speed * cSpeedConst;
            }

            return returnValue;
        }

        //'User name from id
        public string fUserName(long id)
        {
            string query = "SELECT uName FROM tbluser WHERE uID = " + id;

            OdbcCommand cmdfUserName = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader ReaderfUserName = cmdfUserName.ExecuteReader();

            string returnValue;

            if (ReaderfUserName.HasRows)
            {
                returnValue = Convert.ToString(ReaderfUserName["uName"]);
            }
            else
            {
                returnValue = "Unknown";
            }

            ReaderfUserName.Close();

            return returnValue;
        }

        //'Base name from baseid
        public string fBaseName(long id)
        {
            string query = "SELECT basName FROM tblbase WHERE basID = " + id;

            OdbcCommand cmdfBaseName = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader ReaderfBaseName = cmdfBaseName.ExecuteReader();

            string returnValue;

            if (ReaderfBaseName.HasRows)
            {
                returnValue = Convert.ToString(ReaderfBaseName["basName"]);
            }
            else
            {
                returnValue = "Unknown";
            }

            ReaderfBaseName.Close();

            return returnValue;

        }


        //Update resources and money for a user in all bases with units in
        public void bas_res_user(decimal ownerUserID)
        {

            string query = "SELECT tbluobj.uObjbaseID, tblbase.basUserID FROM tbluobj INNER JOIN tblbase ON uObjBaseID = basID WHERE tbluobj.uObjuID = " + ownerUserID + " GROUP BY uObjBaseID";
            OdbcCommand cmd = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader MyReader = cmd.ExecuteReader();
            while (MyReader.Read())
            {
                //Console.WriteLine(MyReader["basUserID"].ToString());
            }

            MyReader.Close();
            MyReader = null;
            //Set Rs2 = Connect.Execute(query)

            //Do While Not Rs2.EOF
            //    userID = Rs2("basUserID")
            //    baseID = Rs2("uObjBaseID")
            //    resStoreLevel = resStoreLvl(userID, baseID)
            //    basRes=bas_res(userID,baseID,Round((resInc(userID,baseID))/60,3),bas_cost(userID,baseID),resSpace(resStoreLevel))
            //    Rs2.MoveNext
            //Loop

        }



        //'Calculate resources storage level. 
        public long resStoreLvl(long uID, long baseID)
        {
            string query = "SELECT basType FROM tblbase WHERE basUserID = " + uID + " AND basID = " + baseID;

            OdbcCommand cmd = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            OdbcDataReader MyReader = null;
            MyReader = cmd.ExecuteReader();

            long basType;

            if (MyReader.HasRows)
            {
                basType = Convert.ToInt64(MyReader["basType"].ToString());
            }
            else
            {
                basType = 1;
            }

            int resStoreID;
            if (basType == 1)
            {
                resStoreID = 17;
            }
            else if (basType == 2)
            {
                resStoreID = 18;
            }
            else
            {
                resStoreID = 17;
            }

            MyReader.Close();
            MyReader = null;

            query = "SELECT uObjLevel FROM tbluobj WHERE uObjID = " + resStoreID + " AND uObjuID = " + uID + " AND uObjBaseID = " + baseID;
            OdbcCommand cmd2 = new OdbcCommand(query, ConnectToMySqlObj.myConn);
            MyReader = cmd2.ExecuteReader();

            long returnValue;
            if (MyReader.HasRows)
            {
                returnValue = Convert.ToInt64(MyReader["uObjLevel"].ToString());
            }
            else
            {
                returnValue = 0;
            }

            MyReader.Close();
            MyReader = null;

            return returnValue;
        }

        //End of Functions from connect.asp

    }

}
