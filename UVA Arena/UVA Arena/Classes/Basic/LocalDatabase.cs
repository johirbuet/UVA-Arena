﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UVA_Arena.Structures;

namespace UVA_Arena
{
    internal static class LocalDatabase
    {
        public static bool IsReady = false;
        public static UserInfo DefaultUser;
        public static List<ProblemInfo> problem_list;
        public static Dictionary<long, long> problem_id;
        public static Dictionary<long, ProblemInfo> problem_num;
        public static Dictionary<long, List<ProblemInfo>> problem_vol;
        public static Dictionary<string, List<ProblemInfo>> problem_cat;
        public static Dictionary<string, string> usernames;

        #region Loader Functions

        /// <summary> Load the database from downloaded data </summary>
        public static void LoadDatabase()
        {
            RunLoadAsync(true);
        }

        public static void RunLoadAsync(object background)
        {
            if ((bool)background)
            {
                bool back = System.Threading.ThreadPool.QueueUserWorkItem(RunLoadAsync, false);
                if (back) return;
            }

            try
            {
                IsReady = false;

                //load new problem list
                string text = File.ReadAllText(LocalDirectory.GetProblemDataFile());
                List<List<string>> data = JsonConvert.DeserializeObject<List<List<string>>>(text);
                if (data == null || data.Count == 0)
                    throw new NullReferenceException("Problem database was empty");

                LoadList(data);
                LoadOthers();
                data.Clear();

                IsReady = true;
                LoadDefaultUser();
            }
            catch (Exception ex)
            {
                Logger.Add(ex.Message, "Problem Database|RunLoadAsync()");
            }

            IsReady = true;
            Interactivity.ProblemDatabaseUpdated();
        }

        private static void LoadList(List<List<string>> datalist)
        {
            //set values 
            problem_list = new List<ProblemInfo>();
            problem_id = new Dictionary<long, long>();
            problem_num = new Dictionary<long, ProblemInfo>();
            problem_vol = new Dictionary<long, List<ProblemInfo>>();
            problem_cat = new Dictionary<string, List<ProblemInfo>>();

            //Load problem from list
            foreach (List<string> lst in datalist)
            {
                ProblemInfo plist = new ProblemInfo(lst);
                problem_list.Add(plist);

                SetProblem(plist.pnum, plist);
                SetNumber(plist.pid, plist.pnum);
                GetVolume(plist.volume).Add(plist);
                foreach (string cat in plist.tags)
                {
                    GetCategory(cat).Add(plist);
                }
            }
        }

        private static void LoadOthers()
        {
            if (problem_list.Count <= 10) return;

            //set favorites
            foreach (long pnum in RegistryAccess.FavoriteProblems)
            {
                if (HasProblem(pnum))
                {
                    GetProblem(pnum).marked = true;
                }
            }

            //get all dacu
            SortedList<long, int> AllDacu = new SortedList<long, int>();
            foreach (ProblemInfo plist in problem_list)
            {
                if (AllDacu.ContainsKey(plist.dacu))
                    AllDacu[plist.dacu] += 1;
                else
                    AllDacu.Add(plist.dacu, 1);
            }

            //cumulative sum of all dacu
            int last = 0;
            Dictionary<long, int> position = new Dictionary<long, int>();
            foreach (long key in AllDacu.Keys)
            {
                last += AllDacu[key];
                position.Add(key, last);
            }
            AllDacu.Clear();

            //set problem level 
            int product = problem_list.Count / 10;
            foreach (ProblemInfo plist in problem_list)
            {
                double rank = 10 * (1 - (double)position[plist.dacu] / problem_list.Count);
                if (position[plist.dacu] + 50 > problem_list.Count) rank -= 1;
                else if (position[plist.dacu] + 100 > problem_list.Count) rank -= 0.5;

                double ac = plist.total <= 0 ? 0 : (double)plist.ac / plist.total;
                if (ac > 0.6) rank -= 1;
                else if (ac > 0.4) rank -= 0.5;
                if (2 * ac < 1 && position[plist.dacu] + 100 < problem_list.Count)
                    rank += 2 * (1 - 2 * ac);                
                plist.level = 2 + rank;
            }
            position.Clear();
        }

        public static void LoadDefaultUser()
        {
            string user = RegistryAccess.DefaultUsername;
            string file = LocalDirectory.GetUserSubPath(user);
            string data = File.ReadAllText(file);
            DefaultUser = JsonConvert.DeserializeObject<UserInfo>(data);
            if (DefaultUser != null) DefaultUser.Process();
        }

        public static void LoadCatagories()
        {
            string file = LocalDirectory.GetCategoryPath();
            string data = File.ReadAllText(file);
            List<ContextBook> catlist = JsonConvert.DeserializeObject<List<ContextBook>>(data);
            if (catlist == null) return;
            foreach (ContextBook book in catlist)
            {
                book.Process();
            }
        }

        #endregion

        #region Other Functions

        /// <summary> Save problem number for given problem id </summary>
        public static void SetNumber(long pid, long pnum)
        {
            if (problem_id == null) return;
            if (problem_id.ContainsKey(pid)) return;
            problem_id.Add(pid, pnum);
        }
        /// <summary> Get problem number for given problem id </summary>
        public static long GetNumber(long pid)
        {
            if (problem_id == null) return -1;
            if (!problem_id.ContainsKey(pid)) return 0;
            return problem_id[pid];
        }

        /// <summary> Get whether given problem number exist </summary>
        public static bool HasProblem(long pnum)
        {
            if (problem_num == null) return false;
            return problem_num.ContainsKey(pnum);
        }
        /// <summary> Save problem info for given problem number </summary>
        public static void SetProblem(long pnum, ProblemInfo plist)
        {
            if (problem_num == null) return;
            if (HasProblem(pnum)) problem_num[pnum] = plist;
            else problem_num.Add(pnum, plist);
        }
        /// <summary> Get problem info for given problem number </summary>
        public static ProblemInfo GetProblem(long pnum)
        {
            if (!HasProblem(pnum)) return null;
            return problem_num[pnum];
        }

        /// <summary> Get problem title for given problem number </summary>
        public static string GetTitle(long pnum)
        {
            if (!HasProblem(pnum)) return "-";
            return GetProblem(pnum).ptitle;
        }
        /// <summary> Get problem id for given problem number </summary>
        public static long GetProblemID(long pnum)
        {
            if (!HasProblem(pnum)) return 0;
            return GetProblem(pnum).pid;
        }

        /// <summary> Get problem list for given volume </summary>
        public static List<ProblemInfo> GetVolume(long vol)
        {
            if (problem_vol == null) return null;
            if (problem_vol.ContainsKey(vol)) return problem_vol[vol];
            problem_vol.Add(vol, new List<ProblemInfo>());
            return problem_vol[vol];
        }
        /// <summary> Get problem list for given category </summary>
        public static List<ProblemInfo> GetCategory(string cat)
        {
            if (problem_cat == null) return null;
            if (problem_cat.ContainsKey(cat)) return problem_cat[cat];
            problem_cat.Add(cat, new List<ProblemInfo>());
            return problem_cat[cat];
        }

        /// <summary> check if this user contains in the list </summary>
        public static bool ContainsUsers(string user)
        {
            return usernames.ContainsKey(user);
        }
        /// <summary> get user id from name </summary>
        public static string GetUserid(string name)
        {
            if (!ContainsUsers(name)) return "";
            return usernames[name];
        }

        #endregion

    }
}
