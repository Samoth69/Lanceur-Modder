//MainWindow.xaml.cs Violent Thomas VS2017 06/2019

//l'objectif de ce projet était de facilité l'installation de "modpack" pour le jeu Minecraft simplifié du côté des joueurs
//en leur permettant de sélectionner 1 bouton et de pouvoir télécharger puis lancer le jeu
//les mises à jour sont aussi pris en charge par le logiciel

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using WinSCP;
using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Lanceur_modder
{
/// <summary>
/// Logique d'interaction pour MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
    {
        /*
         *  fonction pour déterminer l'accès à internet 
         */
        [DllImport("wininet.dll")]
        private extern static bool InternetGetConnectedState(out int description, int reservedValue);

        public static bool IsInternetAvailable()
        {
            int description;
            return InternetGetConnectedState(out description, 0);
        }

        /*
         *  Paramètre SFTP
         */

        //chemin sur le serveur
        const string SrvPath = "/";

        //chemin côté client
        const string InstanceSrvPath = "/Instances/";

        SessionOptions sessionOptions = new SessionOptions
        {
            Protocol = Protocol.Ftp,
            HostName = "****",
            UserName = "****",
            Password = "****",
            PortNumber = 1234,
        };

        //liste des modpacks
        public struct ModPacks
        {
            public string Nom { get; set; } //nom du modpack
            public string Path { get; set; } //nom du dossier contenant le modpack
            public string Version { get; set; } //numéro de la version (si ce dernier est différent de celle du serveur alors maj)
            public Array Folders { get; set; } //liste des dossiers du modpacks (les fichiers dans ses dossiers sont inclus)
            public Array Files { get; set; } //liste des fichiers du modpacks
        };

        //liste des modpacks (20 est une limite arbitraire)
        ModPacks[] MP = new ModPacks[20];

        //quantité de ram présent sur la machine (exprimé en GB)
        double ram;

        //vrai = La machine à accès à internet
        //faux = La machine n'a pas accès à internet
        bool InternetAccess = false;

        //fenêtre de chargement quand le logiciel démare
        LoadingScreen BootScreen = new LoadingScreen();

        /*
         *  code principale
         */

        //revoit le chemin de base (.samoth dans appdata)
        private string DataPath() => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) , ".Samoth");
        
        //renvoit le chemin du dossier où il y a toutes les instances (ou modpack)
        private string InstancesPath() => DataPath() + "\\instances\\";

        #region interface

        //code effectuer AVANT l'affichage de la fenêtre principale
        public MainWindow()
        {
            InitializeComponent();
            InitializeBackgroundWorker();

            BootScreen.Show();

            //vérifie si le fichier log existe.
            if (File.Exists("debug.log"))
            {
                try
                {
                    File.Delete("debug.log");
                    Debug("fichier debug.log effacé");
                }
                catch
                {
                    Debug("erreur lors de l'effacement de debug.log");
                }
            }
            Debug("Starting", true);

            //vérifie si le logiciel à accès à internet
            Debug("Vérification accès à internet", true);
            if (!IsInternetAvailable())
            {
                if (MessageBox.Show("L'ordinateur semble ne pas avoir d'accès à internet.\nImpossible de mettre à jour la liste des ModPacks", "Erreur", MessageBoxButton.OKCancel, MessageBoxImage.Error) == MessageBoxResult.Cancel)
                {
                    System.Windows.Application.Current.Shutdown();
                }
                Debug("pas d'accès internet, démarage en mode hors-ligne", true);
                InternetAccess = false;
            } else {
                Debug("Internet semble être accessible, youpi !", true);
                InternetAccess = true;
            }

            Debug("%APPDATA%=" + DataPath()); //affiche le chemin appdata en debug

            //vérifie si le dossier principal de l'application existe
            Debug("Vérification si [" + DataPath() + "] Existe", true);
            if (Directory.Exists(DataPath()) == false)
            {
                Debug("Création dossier Appdata",true);
                Directory.CreateDirectory(DataPath());
            }

            if (InternetAccess)
            {
                try
                {
                    Debug("Connection FTP...", true);
                    using (Session session = new Session()) //note: il n'y à pas besoin de demander explicitement de fermer la connection car quand on sort du using il le fait automatiquement
                    {
                        // Connect
                        session.Open(sessionOptions);

                        //télécharge la liste des packs présent sur le serveur
                        Debug("Téléchargement Fichier packs.list", true);
                        session.GetFiles(SrvPath + "packs.list", DataPath() + "\\");
                        Debug("Done", true);
                    }

                    //ouvre le fichier qui est au format JSON
                    Debug("Ouverture fichier packs.list", true);
                    if (File.Exists(DataPath() + "\\packs.list"))
                    {
                        Debug("Parsing", true);
                        JObject stuff = JObject.Parse(File.ReadAllText(DataPath() + "\\packs.list"));

                        //on s'assure que le fichier est bien compatible avec la version du logiciel
                        if ((string)stuff["FVer"] == "1")
                        {
                            try
                            {
                                Debug("fichier texte version 1", true);

                                //contient la liste des modpacks
                                JArray PKL = (JArray)stuff["packs"];

                                for (int i = 0; i < PKL.Count; i++)
                                {
                                    //on ajoute chaque modpack dans la liste
                                    MP[i].Nom = (string)PKL[i]["nom"];
                                    CB_LMP.Items.Add(MP[i].Nom); //voir binding listbox

                                    MP[i].Path = (string)PKL[i]["path"];
                                    MP[i].Version = (string)PKL[i]["PVer"];

                                    //MP[i].Folders = PKL[0]["LookOutFolders"];
                                    JArray FL = (JArray)PKL[i]["LookOutFolders"];
                                    if (FL != null)
                                    {
                                        MP[i].Folders = FL.ToArray();
                                    }

                                    //MP[i].Files = PKL[0]["LookOutFiles"];
                                    //FL = (JArray)PKL[i]["LookOutFiles"];
                                    //MP[i].Files = FL.ToArray();

                                    FL = (JArray)PKL[i]["LookOutFiles"];
                                    if (FL != null)
                                    {
                                        MP[i].Files = FL.ToArray();
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Debug(e.ToString());
                            }
                        }
                        else
                        {
                            Debug("Fichier packs.list non reconnu\nle client est potentiellement pas à jour\ncontacter Samoth pour demander plus d'information");
                            MessageBox.Show("Fichier packs.list non reconnu\nle client est potentiellement pas à jour\ncontacter Samoth pour demander plus d'information");
                        }

                    }

                }
                catch (Exception e)
                {
                    MessageBox.Show("Erreur lors du téléchargement du fichier des modpacks (serveur Down ?)\n" + e.ToString());
                    Debug("Erreur lors du téléchargement du fichier des modpacks (serveur Down ?)\n" + e.ToString());
                }
            }
            else
            {
                //si jamais il n'y à pas d'accès à internet,
                //le logiciel cherche dans ses dossiers les modpacks existant
                //ce dernier supose que les modpacks sont totalement installés
                Debug("Mode HorsLigne.", true);
                if (Directory.Exists(InstancesPath()))
                {
                    if (Directory.GetDirectories(InstancesPath()).Count() != 0)
                    {
                        Debug("Listing instances locals", true);
                        int count = 0;
                        foreach (string i in Directory.GetDirectories(InstancesPath()))
                        {
                            if (File.Exists(i + "\\version.txt")) {
                                string[] data = File.ReadAllLines(i + "\\version.txt");
                                MP[count].Nom = data[0];
                                CB_LMP.Items.Add(MP[count].Nom);

                                MP[count].Path = i;
                                MP[count].Version = data[2];
                                count++;
                            }
                        }
                    }
                    else
                    {
                        Debug("dossier instance vide ! (0 dossier dans instances");
                    }
                }
                else
                {
                    Debug("dossier instance non existant");
                }
            }

            //lecture configuration
            try
            {
                Debug("Ouverture fichier config", true);
                string[] data = new string[2];

                //si le fichier config existe, on l'ouvre
                //sinon on le créer avec des paramètres par défaut
                if (File.Exists(DataPath() + "\\config.ini"))
                {
                    //ram = Convert.ToDouble(File.ReadAllText(DataPath() + "\\config.ini"));

                    Debug("lecture fichier config", true);
                    data = File.ReadAllLines(DataPath() + "\\config.ini");
                }
                else
                {
                    Debug("le fichier config n'existe pas. creation et assignation de valeur par défaut", true);
                    File.Create(DataPath() + "\\config.ini");
                    data[0] = "";
                    data[1] = "2";
                }
                try
                {
                    CB_LMP.SelectedIndex = Convert.ToInt16(data[0]);
                    ram = Convert.ToDouble(data[1]);
                }
                catch (Exception e)
                {
                    Debug("Erreur lors de l'assignation des valeurs dans le fichier config: " + e.ToString());
                }

                L_Main.Content = "Prêt";
            }
            catch (Exception e)
            {
                MessageBox.Show("Erreur lors du chargement du fichier config\n" + e.ToString());
                Debug("Erreur lors du chargement du fichier config\n" + e.ToString());
            }
            BootScreen.Close();
        }

        //public static MainWindow Instance { get; private set; }

        //affiche des messages avec heure et date dans la ListBox
        //entrée: msg: message à afficher
        public void Debug(string msg)
        {
            //vérifie si l'élément qui appel la fonction à accès à la fenêtre
            //sinon invoke un dispatcher pour intéragir avec la fenêtre
            if (this.Dispatcher.CheckAccess())
            {
                this.LB_Debug.Items.Add(DateTime.Now + ": " + msg);
                File.AppendAllText("debug.log", DateTime.Now + ": " + msg + "\n");
            }
            else
            {
                this.Dispatcher.Invoke(() =>
                {
                    this.LB_Debug.Items.Add(DateTime.Now + ": " + msg);
                    File.AppendAllText("debug.log", DateTime.Now + ": " + msg + "\n");
                });
            }
        }

        //affiche des messages avec heure et date dans la ListBox
        //entrées: msg: message à écrire en debug
        public void Debug(string msg, bool Starting_screen)
        {
            this.LB_Debug.Items.Add(DateTime.Now + ": " + msg);
            File.AppendAllText("debug.log", DateTime.Now + ": " + msg + "\n");
            BootScreen.UpdateText(msg);
        }

        //Affiche la fenêtre d'option
        private void BT_Options_Click(object sender, RoutedEventArgs e)
        {
            Config screen = new Config();
            screen.SL_RAM.Value = ram;
            screen.ShowDialog();
            ram = screen.SL_RAM.Value;
            //MessageBox.Show(ram.ToString());
        }

        private void Window_Closing(object sender, EventArgs e)
        {
            string[] data = new string[2];
            data[0] = CB_LMP.SelectedIndex.ToString();
            data[1] = ram.ToString();
            File.WriteAllLines(DataPath() + "\\config.ini", data);
        }

        private void Lancer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Lancer.IsEnabled = false; //évite les spam click
                if (!Directory.Exists(InstancesPath()))
                {
                    Directory.CreateDirectory(InstancesPath());
                }
                string InstancePath = InstancesPath() + MP[CB_LMP.SelectedIndex].Path;
                if (!Directory.Exists(InstancePath))
                {
                    Directory.CreateDirectory(InstancePath);
                }

                //lance le backgroundworker pour assurer le téléchargement des ressources (afin d'éviter de freezer la fenêtre pendant le téléchargement)
                backgroundWorker1.RunWorkerAsync(MP[CB_LMP.SelectedIndex]);

            }
            catch (Exception f)
            {
                //Lancer.IsEnabled = true;
                Debug("Erreur dans boucle lancement: " + f.ToString());
            }
            Lancer.IsEnabled = true;
        }

        //écrit le fichier d'information pour chaque modpack (sera écrit dans un fichier nommé version.txt)
        //entrées: path: chemin du DOSSIER dans lequel écrire le fichier
        //         struc: object qui contient les informations du modpack
        private void PrintVersion(string path, ModPacks struc)
        {
            try
            {
                string[] data = new string[1];
                data[0] = struc.Nom;
                data[1] = struc.Version;

                File.WriteAllLines(path + "version.txt", data);
            }
            catch (Exception e)
            {
                if (this.Dispatcher.CheckAccess())
                {
                    Debug("Erreur dans l'écriture de la version\n" + e.ToString());
                }
                else
                {
                    Debug2("Erreur dans l'écriture de la version\n" + e.ToString());
                }
            }
        }

        #endregion

        #region backgroundWorker
        /// <summary>
        /// le backgroundWorker permet d'executer du code qui prend du temps sans figer l'interface utilisateur.
        /// il sert l'orsque l'utilisateur lance un modpack et va vérifier si il à jour / installé.
        /// si ce dernier n'est pas à jour ou pas installé
        /// il téléchargera les fichiers manquant pour lancer le modpack une fois que tout est prêt
        /// </summary>


        //object qui permet de faire tourner du code "en fond"
        //cela permet de faire tourner un code qui prend du temps sans figer l'interface utilisateur
        private System.ComponentModel.BackgroundWorker backgroundWorker1;

        //contient les infos du modpack que l'on shouaite lancer
        ModPacks pak;

        //passe sur vrai quand un erreur ce produit
        //par exemple, une déconnection inatendu avec le serveur
        bool error = false;

        //ct: contient le numéro de l'étape actuel dans le téléchargement (n'a pas d'influence particulière sur le code,
        //    sert uniquement pour indiquer à l'utilisateur ou en est le téléchargement)
        //ctmax: contient le nombre d'étape dans le téléchargement
        int ct, ctmax;

        //fonction utile que pour le backgroundworker
        //le BW ne peux pas manipuler des objets qui ne lui appartiennent pas car il tourne sur un autre coeur du cpu que la fenêtre principal
        //on va donc "invoquer" la fonction, c'est à dire, on demande à la fenêtre d'executer le code pour nous
        private void Debug2(string text)
        {
            this.Dispatcher.Invoke(() =>
            {
                Debug("BGW:" + text);
            });
        }

        // Set up the BackgroundWorker object by 
        // attaching event handlers. 
        private void InitializeBackgroundWorker()
        {
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();

            //executé au moment du lancement du backgroundworker
            backgroundWorker1.DoWork += new DoWorkEventHandler(backgroundWorker1_DoWork);

            //executé lorsque le code DoWork à fini
            backgroundWorker1.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker1_RunWorkerCompleted);
        }

        //code principal backgroundworker
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            //modpack que l'on shouaite lancer
            pak = (ModPacks)e.Argument;

            //ClientVersion: version du modpack côté client
            //SrvVersion: version du modpack sur le serveur
            string ClientVersion, SrvVersion;

            //path: chemin relatif du modpack (par rapport au dossier instances)
            string Path = pak.Path;

            //InstancePath: chemin absolu du modpack
            string InstancePath = InstancesPath() + pak.Path;

            //contenu du fichier version.txt
            string data;

            this.Dispatcher.Invoke(() =>
            {
                PB_Progress.IsIndeterminate = true;
            });

            //récupère la version du modpack côté client, si elle existe. sinon met -1
            if (File.Exists(InstancePath + "\\version.txt"))
            {
                data = File.ReadAllText(InstancePath + "\\version.txt");
                var isNumeric = int.TryParse(data, out int n);
                if (isNumeric)
                {
                    ClientVersion = data;
                }
                else
                {
                    ClientVersion = "-1";
                }
            }
            else
            {
                ClientVersion = "-1";
            }

            if (!InternetAccess && pak.Version == "-1")
            {
                MessageBox.Show("impossible de lancer le modpack car ce dernier n'est pas complet.");
                Debug2("impossible de lancer le modpack car ce dernier n'est pas complet.");
                error = true;
                return; //dans le cas ou internet n'est pas accessible et que le internet n'est pas dispo, on quite la fonction.
            }

            SrvVersion = pak.Version;

            this.Dispatcher.Invoke(() =>
            {
                PB_Progress.IsIndeterminate = false;
            });

            //si la version serveur et client sont identique, on quite la fonction pour lancer le jeu.
            if (ClientVersion == SrvVersion)
            {
                return; //quite la fonction
            }
                

            if (InternetAccess)
            {
                try
                {
                
                    Debug2("Connection SFTP");
                    using (Session session = new Session()) //note: il n'y a pas besoin de demander explicitement de fermer la connection car la sorti de using le fait automatiquement
                    {
                        //event que winscp déclenche lorsqu'il y a du progret dans le téléchargement
                        session.FileTransferProgress += Session_FileTransferProgress;

                        //Connection
                        session.Open(sessionOptions);

                        Debug2("Téléchargement modpack");

                        if (pak.Folders != null)
                        {
                            ct = 0;
                            ctmax = pak.Folders.Length;
                            foreach (JValue a in pak.Folders)
                            {
                                string i;
                                i = a.ToString();
                                Debug2("Début téléchargement dossier: from [" + InstanceSrvPath + Path + "/" + i + "] to [" + InstancePath + "\\" + i.Replace("/", "\\") + "]");
                                if (!Directory.Exists(InstancePath + "\\" + i.Replace("/", "\\")))
                                {
                                    Directory.CreateDirectory(InstancePath + "\\" + i.Replace("/", "\\"));
                                }
                                session.SynchronizeDirectories(SynchronizationMode.Local, InstancePath + "\\" + i.Replace("/", "\\") + "\\", InstanceSrvPath + Path + "/" + i + "/", true, true);
                                ct++;
                            }
                        }
                        else
                        {
                            Debug2("pas de dossier définie en MAJ");
                        }

                        if (pak.Files != null)
                        {
                            ct = 0;
                            ctmax = pak.Files.Length;
                            foreach (JValue a in pak.Files)
                            {
                                string i;
                                i = a.ToString();
                                Debug2("Début téléchargement fichier: from [" + InstanceSrvPath + Path + "/" + i + "] to [" + InstancePath + "\\" + i.Replace("/", "\\") + "]");
                                if (session.FileExists(InstanceSrvPath + Path + "/" + i)) {
                                    RemoteFileInfo fichier = session.GetFileInfo(InstanceSrvPath + Path + "/" + i);
                                    if (fichier.LastWriteTime > File.GetLastWriteTime(InstancePath + "\\" + i.Replace("/", "\\")))
                                    {
                                        session.GetFiles(InstanceSrvPath + Path + "/" + i, InstancePath + "\\" + i.Replace("/", "\\"));
                                    }
                                    else
                                    {
                                        Debug2("skipped (version local plus récente ou version serveur inchangé)");
                                    }
                                }
                                else
                                {
                                    Debug2("fichier absent coté serveur (" + i + ")");
                                }
                                ct++;
                            }
                        }
                        else
                        {
                            Debug2("pas de fichier définie en MAJ");
                        }

                        File.WriteAllText(InstancePath + "\\version.txt", pak.Version);
                    }

                
                }
                catch (Exception f)
                {
                    MessageBox.Show("Erreur lors du téléchargement des fichiers \n" + f.ToString());
                    Debug2("Erreur lors du téléchargement des fichiers \n" + f.ToString());
                    error = true;
                }
            }
            else
            {
                if (!File.Exists(InstancePath + "\\run.bat"))
                {
                    MessageBox.Show("pas de fichier executable pour le modpack");
                    Debug2("pas de fichier executable pour le modpack (run.bat not found at " + InstancePath + "\\run.bat" + ")");
                    error = true;
                }
            }

        }

        //ce déclenche quand il y a du progrès dans le téléchargement
        //met à jour la barre de progression
        private void Session_FileTransferProgress(object sender, FileTransferProgressEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                PB_Progress.Value = e.OverallProgress;
                L_Main.Content = "[" + ct + "/" + ctmax + "]" + System.IO.Path.GetFileName(e.FileName) + "[" + e.FileProgress * 100 + "%] @" + (e.CPS / 1000) + "Kb/s";
            });
        }

        //ce déclenche quand le téléchargement est fini
        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string InstancePath = InstancesPath() + pak.Path;

            this.Dispatcher.Invoke(() =>
            {
                L_Main.Content = "done";
            });

            if (!error)
            {
                //écrit dans le fichier du launcheur Minecraft les informations nécéssaire
                JObject stuff = JObject.Parse(File.ReadAllText(InstancePath + "\\launcher_profiles.json"));

                //cette ligne assume que l'élément "profiles" existe ! (ce qui devrait être la cas sauf si le fichier est vide)
                if (stuff["profiles"].HasValues)
                {
                    if (stuff["profiles"]["forge"] != null)
                    {
                        string text = "";
                        List<string> content = new List<string>();

                        //enlève les espaces à la fin de chaque élements de la série. (on en profite aussi pour cherche où est l'argument Xmx (si il existe))
                        int JvmArId = -1;

                        //on vérifie si l'objet en dessous existe
                        if (stuff["profiles"]["forge"]["javaArgs"] == null)
                        {
                            stuff["profiles"]["forge"].AddAnnotation("javaArgs");
                            text = "-Xmx2G -XX:+UnlockExperimentalVMOptions -XX:+UseG1GC -XX:G1NewSizePercent=20 -XX:G1ReservePercent=20 -XX:MaxGCPauseMillis=50 -XX:G1HeapRegionSize=32M";
                        }
                        else
                        {
                            text = stuff["profiles"]["forge"]["javaArgs"].ToString();
                        }

                        content.AddRange(text.Split('-'));

                        for (int i = 0; i < content.Count; i++)
                        {
                            if (content[i].Length != 0)
                            {
                                //filtre les espaces avant les arguments
                                if (content[i][content[i].Length - 1] == ' ')
                                {
                                    content[i] = content[i].Remove(content[i].Length - 1, 1);
                                }
                                //si l'argument xmx est trouvé
                                if (content[i].Substring(0, 3).ToLower() == "xmx")
                                {
                                    JvmArId = i;
                                }
                            }
                        }

                        //si l'argument xmx n'est pas trouvé
                        if (JvmArId == -1)
                        {
                            content.Add("");
                            JvmArId = content.Count - 1;
                        }

                        content[JvmArId] = "Xmx" + (int)(ram * 1000) + "M";
                        text = "";


                        foreach (string i in content)
                        {
                            if (i != "")
                            {
                                text += "-" + i + " ";
                            }
                        }

                        stuff["profiles"]["forge"]["javaArgs"] = text;

                        JsonSerializer serializer = new JsonSerializer
                        {
                            NullValueHandling = NullValueHandling.Include,
                            DateFormatHandling = DateFormatHandling.IsoDateFormat,
                            DateTimeZoneHandling = DateTimeZoneHandling.Unspecified,
                            Formatting = Formatting.Indented //ligne optionel (peut utile car pas besoin de compression)
                        };

                        //écrit le fichier
                        using (StreamWriter sw = new StreamWriter(@InstancePath + "\\launcher_profiles.json"))
                        using (JsonWriter writer = new JsonTextWriter(sw))
                        {
                            serializer.Serialize(writer, stuff);
                        }
                    }
                    else
                    {
                        Debug2("élément forge dans profiles du fichier launcher_profiles.json introuvable");
                        MessageBox.Show("Erreur lors de la modification du fichier launcheur_profiles.json");
                    }
                }
                else
                {
                    Debug2("pas de profile launcher_profiles.json");
                    MessageBox.Show("pas de profile dans launcher_profiles.json", "",MessageBoxButton.OK, MessageBoxImage.Error);
                }

                var startInfo = new ProcessStartInfo
                {

                    // Sets RAYPATH variable to "test"
                    // The new process will have RAYPATH variable created with "test" value
                    // All environment variables of the created process are inherited from the
                    // current process

                    //startInfo.EnvironmentVariables["APPDATA"] = DataPath() + "\\instances\\" + pak.Path + "\\.minecraft";
                    //la ligne ci dessus est inutile car le launcheur de minecraft ne fonctionne plus avec cette variable.


                    // Required for EnvironmentVariables to be set
                    UseShellExecute = false, //pas sur de son utilité

                    Arguments = "--workDir \"" + InstancePath + "\"", //ceci est la ligne importante.

                    // Sets some executable name
                    // The executable will be search in directories that are specified
                    // in the PATH variable of the current process
                    FileName = InstancePath + "\\Minecraft.exe"
                };

                // Starts process
                Process.Start(startInfo);
            }

            PrintVersion(InstancePath, pak);
        }
        #endregion
    }
}
