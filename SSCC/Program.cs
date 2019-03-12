using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Net.NetworkInformation;

/*To do:
 * Add control over how long the tread sleeps after receiving a ping response
 * Support for reference connections for better clarity of if a link is down or just a client
 * 
 */

namespace SSCC
{
    class Program
    {
        static string address = "";
        static DateTime end_date = DateTime.Now;
        static DateTime start_date = DateTime.Now;
        static volatile bool status = false;
        static volatile bool connected = false;
        static bool cont = true;
        static string out_file = "";
        static int max_wait = 1000;
        static int dropout_def = 15;

        static void Main(string[] args)
        {
            Setup();
        }

        static void Setup()
        {
            start:;
            Console.WriteLine("Entered monitor setup");
            Console.WriteLine("SSCC - " + DateTime.Now.ToString());

            //Address
            Console.WriteLine();
            Console.Write("Enter hostname or IP to monitor: ");
            address = Console.ReadLine();
            if(address == "exit") { Environment.Exit(0); }

            //report path
            Console.WriteLine();
            Console.Write("Enter out file path: ");
            out_file = Console.ReadLine();
            if (out_file == "exit") { Environment.Exit(0); }
            if (out_file == "new") { goto start; }

            //report length
            length:;
            Console.WriteLine();
            Double add_time = 1;
            Console.Write("Enter how many hours to monitor: ");


            try
            {
                string a = Console.ReadLine();
                if (a == "exit") { Environment.Exit(0); }
                if (a == "new") { goto start; }
                add_time = Convert.ToDouble(a);
            }
            catch (Exception)
            {
                Console.WriteLine("Incorrect data type, please enter a max of one decimal place");
                goto length;
            }

            end_date = DateTime.Now.AddHours(add_time);

            Console.Clear();
            Console.Write("Configure advanced settings?(y/n): ");
            string adv = Console.ReadLine();
            if(adv != "y") { goto adv_exit; }

            // set advanced settings
            
            adv_start:;
            Console.WriteLine();
            Console.Write("Enter timeout in ms: ");
            string timeout_s = Console.ReadLine();
            if (timeout_s == "exit") { Environment.Exit(0); }
            if (timeout_s == "new") { goto start; }
            try
            {
                max_wait = Convert.ToInt32(timeout_s);
            }
            catch (Exception)
            {
                Console.Clear();
                Console.WriteLine("error: was not able to convert \"" + timeout_s + "\" to an integer");
                goto adv_start;
            }

            set_drop_def:;
            Console.WriteLine();
            Console.Write("How many seconds offline is considered a dropout: ");
            string drop = Console.ReadLine();
            if (drop == "exit") { Environment.Exit(0); }
            if (drop == "new") { goto start; }
            try
            {
                dropout_def = Convert.ToInt32(drop);
            }
            catch (Exception)
            {
                Console.Clear();
                Console.WriteLine("error: was not able to convert \"" + drop + "\" to an integer");
                goto set_drop_def;
            }

            adv_exit:;
            Console.Clear();
            Console.WriteLine("Monitoring " + address + " until " + end_date);
            Console.WriteLine("Timeout set to: " + max_wait);
            Console.WriteLine("Dropout defined as " + dropout_def + " seconds");
            Console.WriteLine();
            start_date = DateTime.Now;

            Thread WorkerThread = new Thread(Worker);
            WorkerThread.IsBackground = false;
            WorkerThread.Start();



            idle();
        }

        static void idle()
        {


            Console.WriteLine("Entered idle mode");
            ShowHelp();
            idle:;
            Console.Write(">");
            string a = Console.ReadLine();
            switch (a)
            {
                case "help":
                    Console.Clear();
                    ShowHelp();
                    goto idle;
                case "cls":
                    Console.Clear();
                    goto idle;
                case "status":
                    Console.Clear();
                    Console.WriteLine("Target host: " + address);
                    if (status) { Console.WriteLine("Current thread status: Active"); } else { Console.WriteLine("Current thread status: Inactive"); }
                    Console.WriteLine("Timeout: " + max_wait + "ms");
                    Console.WriteLine("Dropout defined as " + dropout_def + " seconds");
                    Console.WriteLine("Start time: " + start_date);
                    Console.WriteLine("Current time: " + DateTime.Now);
                    Console.WriteLine("End Time: " + end_date);
                    if (connected) { Console.WriteLine("Currently connected"); } else { Console.WriteLine("Currently not connected"); }                    
                    Console.WriteLine();
                    goto idle;
                case "stop":
                    Console.Clear();
                    cont = false;
                    Console.WriteLine("Called for thread stop");
                    break;
                case "exit":
                    Environment.Exit(0);
                    break;
                case "log":
                    string[] logtext = File.ReadAllLines(out_file);
                    foreach(string line in logtext){
                        Console.WriteLine(line);
                    }
                    Console.WriteLine("");
                    goto idle;
                default:
                    goto idle;
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("Commands:");
            Console.WriteLine();
            Console.WriteLine("help: shows all commands");
            Console.WriteLine("new: returns to start of setup, only available while in setup");
            Console.WriteLine("status: gets status of worker thread");
            Console.WriteLine("stop: stops current worker thread & goes to setup");
            Console.WriteLine("exit: closes the app");
            Console.WriteLine("cls: clears the screen");
            Console.WriteLine("log: writes the log file to the console");
            Console.WriteLine();
            Console.WriteLine();
        }

        static void Worker()
        {

            cont = true;
            status = true;
            Ping monitor = new Ping();
            string dest = address;
            DateTime end = end_date;

            int fail_counter = 0;
            DateTime drop_start = DateTime.Now;
            bool log_drop = false;

            File.AppendAllText(out_file, "Target host: " + address + Environment.NewLine);
            File.AppendAllText(out_file, "Timeout: " + max_wait + "ms" + Environment.NewLine);
            File.AppendAllText(out_file, "Dropout defined as " + dropout_def + " seconds");
            File.AppendAllText(out_file, "Log started " + DateTime.Now + Environment.NewLine);
            File.AppendAllText(out_file, "Dropout start time,Dropout end time");

            while (cont && (DateTime.Compare(DateTime.Now, end_date) < 0))
            {

                try
                {
                    PingReply pr = monitor.Send(dest, 4000);

                    if (pr.Status.ToString() == "Success")
                    {
                        connected = true;
                        if (fail_counter > 0 && log_drop)
                        {
                            File.AppendAllText(out_file, Environment.NewLine + drop_start + "," + DateTime.Now);
                        }
                        //reset
                        fail_counter = 0;
                        log_drop = false;
                    }
                    else
                    {
                        connected = false;
                        fail_counter++;
                        if (fail_counter == 1) { drop_start = DateTime.Now; }
                        //if dropout has lasted 15 seconds or more
                        if (DateTime.Compare(drop_start.AddSeconds(dropout_def), DateTime.Now) <= 0)
                        {
                            log_drop = true;
                        }
                    }
                    Thread.Sleep(1000);
                }
                catch (Exception)
                {
                    //if there's an exception it will be the ping method, as such run the fail condition & sleep thread
                    connected = false;
                    fail_counter++;
                    if (fail_counter == 1) { drop_start = DateTime.Now; }
                    //if dropout has lasted 15 seconds or more
                    if (DateTime.Compare(drop_start.AddSeconds(dropout_def), DateTime.Now) <= 0)
                    {
                        log_drop = true;
                    }
                    Thread.Sleep(1000);
                }

            }
            if(log_drop){ File.AppendAllText(out_file, Environment.NewLine + drop_start + "," + DateTime.Now + " - Logged due to end of monitor, connectivity not restored"); }
            File.AppendAllText(out_file, Environment.NewLine + "Log end " + DateTime.Now);
            status = false;
            Console.WriteLine("thread stopped");
            Setup();
        }

    }
}
