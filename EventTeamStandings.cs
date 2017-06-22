using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Text;
using imLeagues.Resources;

namespace imLeagues.Business
{
    public class EventTeamStandings
    {
        public enum Scope
        {
            Sport, SportWithoutWaitlist, League, LeagueWaitlist, LeagueWithoutWaitlist, Division, Team, Group, SportEventTournament
        }

        private Scope _entityType;
        private string _entityID;
        private Sport _sport;
        private StandingOptions[] _standingOptions;
        private int _h2hNdx = -1;
        private string _orderBy = string.Empty;

        private string[] _assignedPointValues = new string[0];
        public string[] AssignedPointValues
        {
            get
            {
                if (!string.IsNullOrEmpty(_sport.AssignedPointValues))
                {
                    _assignedPointValues = _sport.AssignedPointValues.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                }

                return _assignedPointValues;
            }
        }

        public StandingOptions[] OrderByOptions
        {
            get { return _standingOptions; }
            set
            {
                _standingOptions = value;

                // rebuild order by options
                BuildStandingOrderBy();
            }
        }

        public List<string> SportEventTournamentTeams
        {
            get;
            set;
        }

        public int BasedOnRound
        {
            get;
            set;
        }

        public bool DiscardOrderWithinTeamName
        {
            get;
            set;
        }

		private static Dictionary<StandingOptions, string> s_standingDisplayNameDict = new Dictionary<StandingOptions, string>();
		private static Dictionary<StandingOptions, Type> s_standingDataTypeDict = new Dictionary<StandingOptions, Type>();

		/// <summary>
		/// initialize the static variables in static contructor for thread-safe; the old way will cause 
		/// issues in multi-thread environment. It may happen that the first entry TOTAL may not be added the dictionary
		/// finally. TOTAL could be added to the dictionary in one thread, then be overridden by a new dictionary in another
		/// thread
		/// </summary>
		static EventTeamStandings()
		{
			// initialize standing display name dictionary
            s_standingDisplayNameDict.Add(StandingOptions.TOTAL, Strings.EventTeamStandings_Total);
			s_standingDisplayNameDict.Add(StandingOptions.AVERAGE, Strings.EventTeamStandings_Average);
			s_standingDisplayNameDict.Add(StandingOptions.BEST_P, Strings.EventTeamStandings_Best);
			s_standingDisplayNameDict.Add(StandingOptions.WORST_P, Strings.EventTeamStandings_Worst);

			// initialize stand data type dictionary
			s_standingDataTypeDict.Add(StandingOptions.SR, typeof(double));
			s_standingDataTypeDict.Add(StandingOptions.TOTAL, typeof(double));
			s_standingDataTypeDict.Add(StandingOptions.AVERAGE, typeof(double));
			s_standingDataTypeDict.Add(StandingOptions.BEST_P, typeof(int));
			s_standingDataTypeDict.Add(StandingOptions.WORST_P, typeof(int));
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityID"></param>
        /// <param name="entityType"></param>
        public EventTeamStandings(string entityID, Scope entityType)
        {
            _entityID = entityID;
            _entityType = entityType;

            switch ((int)_entityType)
            {
                case (int)Scope.Sport:
                case (int)Scope.SportWithoutWaitlist:
                    _sport = Sport.Create(entityID);
                    break;
                case (int)Scope.League: // league
                case (int)Scope.LeagueWaitlist: // league
                case (int)Scope.LeagueWithoutWaitlist: // league
                    League league = League.Create(_entityID);
                    _sport = Sport.Create(league.SportId);
                    break;
                case (int)Scope.Division: // division
                    Division div = Division.Create(_entityID);
                    _sport = Sport.Create(div.SportId);
                    break;
                case (int)Scope.Team:
                    Team team = Team.Create(entityID);
                    _sport = Sport.Create(team.SportId);
                    break;
                case (int)Scope.SportEventTournament:
                    SportEventTournament sportEventTournament = SportEventTournament.Create(entityID);
                    var curleague = League.Create(sportEventTournament.LeagueId);
                    _sport = Sport.Create(curleague.SportId);
                    break;
                case (int)Scope.Group:
                    Group group = Group.Create(entityID);
                    _sport = Sport.Create();// we don't need sport as group teams can be from different sports
                    break;
                default:
                    throw new Exception(Strings.EventTeamStandings_NotSupported);
            }

            if (_sport.Exists)
            {
                _standingOptions = new StandingOptions[1];
                _standingOptions[0] = _sport.Standings1;
            }
            else
            {
                _standingOptions = new StandingOptions[0];
            }

            BuildStandingOrderBy();
        }

        public static string GetStandingDisplayName(StandingOptions option)
        {
			if (s_standingDisplayNameDict.ContainsKey(option))
            {
				return s_standingDisplayNameDict[option];
            }
            else
            {
				Helper.LogException(new Exception("GetStandingDisplayName"), option.ToString(), s_standingDisplayNameDict.Keys.Count.ToString(), "GetStandingDisplayName");
                return StandingOptions.TOTAL.ToString();
            }
        }

        public static Type GetStandingDataType(StandingOptions option)
        {
			if (s_standingDataTypeDict.ContainsKey(option))
            {
				return s_standingDataTypeDict[option];
            }
            else
            {
				Helper.LogException(new Exception("GetStandingDataType"), option.ToString(), s_standingDataTypeDict.Keys.Count.ToString(), "GetStandingDataType");
                return typeof(double);
            }
        }

        public static string GetStandingsToolTip(StandingOptions option)
        {
            switch (option)
            {
                case StandingOptions.TOTAL:
                    return Strings.EventTeamStandings_TotalPoints;
                case StandingOptions.AVERAGE:
                    return Strings.EventTeamStandings_AveragePoints;
                case StandingOptions.BEST_P:
                    return Strings.EventTeamStandings_BestFinishingPosition;
                case StandingOptions.WORST_P:
                    return Strings.EventTeamStandings_WorstFinishingPosition;
                default:
                    return "";
            }
        }

        public static string getStandingsToolTip(string _standing)
        {
            switch (_standing)
            {
                case "TOTAL":
                    return Strings.EventTeamStandings_TotalPoints;
                case "AVERAGE":
                    return Strings.EventTeamStandings_AveragePoints;
                case "BEST_P":
                    return Strings.EventTeamStandings_BestFinishingPosition;
                case "WORST_P":
                    return Strings.EventTeamStandings_WorstFinishingPosition;
                default:
                    return "";
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="options">standing options</param>
        /// <returns></returns>
        public string GetStandingsHTML(StandingOptions[] options)
        {
            DataTable divSSTable = GetStandingsTable(options);

            StringBuilder returnHTML = new StringBuilder("");
            returnHTML.Append("<table width=\"100%\" class='table table-striped table-condensed' >");

            // Header Row generation for the table
            returnHTML.Append("<tr>");
            returnHTML.Append("<th>"+Strings.EventTeamStandings_Team+"</th>");
            for (int i = 0; i < options.Length; i++)
            {
                returnHTML.Append("<th><a data-original-title='" + GetStandingsToolTip(options[i]) + "'>" + GetStandingDisplayName(options[i]) + "</a></th>");
            }
            returnHTML.Append("</tr>");
            // End Header Row Generation

            for (int i = 0; i < divSSTable.Rows.Count; i++)
            {
                DataRow teamRow = divSSTable.Rows[i];

                TeamStatisticsVO teamVO = (TeamStatisticsVO)teamRow["TeamVO"];

                // Build standings for one team in a row
                returnHTML.Append("<tr align=\"center\" " + ((i % 2 != 0) ? "style=\"background-color: AliceBlue;\"" : "") + ">");
                returnHTML.Append("<td> <div  style=\"text-align: left;\" ><a href=\"" + Helper.GetApplicationPath() + "School/Team/Home.aspx?TeamId=")
                    .Append(teamRow["Id"].ToString())
                    .Append("\">")
                    //.Append(teamRow["Type"].ToString() == "-1" ? "<strike>" : "")
                    .Append(teamRow["Name"].ToString())
                    //.Append(teamRow["Type"].ToString() == "-1" ? "</strike>" : "")
                    .Append("</a></div></td>");

                for (int j = 0; j < options.Length; j++)
                {
                    returnHTML.Append("<td>").Append(teamVO.GetStandingDisplayValue(options[j])).Append("</td>");
                }
                returnHTML.Append("</tr>");
            }

            returnHTML.Append("</table>");

            return returnHTML.ToString();
        }

        /// <summary>
        /// Retrieve a team statistic information
        /// </summary>
        /// <param name="teamID"></param>
        /// <returns>null if the team can NOT be found</returns>
        public TeamStatisticsVO GetTeamStanding(string teamID)
        {
            // dummy options, no useful in this case since TeamStatisticsVO
            // contains all statistical information for the team
            StandingOptions[] standingArray = { StandingOptions.WLT };
            DataTable standingsTable = GetStandingsTable(standingArray);
            foreach (DataRow teamRow in standingsTable.Rows)
            {
                if (teamRow["Id"].Equals(teamID))
                {
                    return (TeamStatisticsVO)teamRow["TeamVO"];
                }
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id">entity id</param>
        /// <param name="type">
        ///     0 - diviison
        ///     1 - league
        /// </param>
        /// <param name="standingArray"></param>
        /// <returns></returns>
        public DataTable GetStandingsTable(StandingOptions[] standingArray)
        {

            List<StandingOptions> standingList = new List<StandingOptions>();
            foreach (StandingOptions item in standingArray)
            {
                standingList.Add(item);
            }

            // Build structure of division standing table
            DataTable divSSTable = new DataTable();
            divSSTable.Columns.Add("TeamVO", typeof(TeamStatisticsVO));
            divSSTable.Columns.Add("Id", typeof(string));
            divSSTable.Columns.Add("Name", typeof(string));
            divSSTable.Columns.Add("Type", typeof(int));
           

            for (int i = 0; i < _standingOptions.Length; i++)
            {
                if (!standingList.Contains(_standingOptions[i])) standingList.Add(_standingOptions[i]);
            }

            //// check to see if we need to calculate period scores for teams
            //if (standingList.Contains(StandingOptions.DIFF_P)
            //    || standingList.Contains(StandingOptions.PA_P)
            //    || standingList.Contains(StandingOptions.PF_P))
            //{
            //    this.IsCountPeriodScores = true;
            //}

            for (int i = 0; i < standingList.Count; i++)
            {
                DataColumn standingCol = new DataColumn(standingList[i].ToString(), GetStandingDataType(standingList[i]));
                standingCol.Caption = GetStandingDisplayName(standingList[i]);
                divSSTable.Columns.Add(standingCol);
            }

            divSSTable.Columns.Add("DivisionName", typeof(string));
            divSSTable.Columns.Add("LeagueName", typeof(string));
            divSSTable.Columns.Add("SportName", typeof(string));
            divSSTable.Columns.Add("LeagueAllowTeamJoin", typeof(bool));
            divSSTable.Columns.Add("IsReplaced", typeof(int));
            divSSTable.Columns.Add("LadderRank", typeof(int));
            divSSTable.Columns.Add("CreateDate", typeof(DateTime));

            // Get statistics information for teams in the division
            // We don't have GB data yet since it can only be calculated after 
            // teams are ranked in a way.
            List<TeamStatisticsVO> divSSTeamList = GetTeamStatisticsList();
            if (divSSTeamList.Count > 0)
            {
                foreach (TeamStatisticsVO vo in divSSTeamList)
                {
                    DataRow newTeamRow = divSSTable.NewRow();
                    newTeamRow["TeamVO"] = vo;
                    newTeamRow["Id"] = vo.TeamID;
                    newTeamRow["Name"] = vo.TeamName;
                    newTeamRow["Type"] = 0; // vo.Type; // 0 is a dummy value. We may not use this field.

                    for (int i = 0; i < standingList.Count; i++)
                    {
                        newTeamRow[i + 4] = vo.GetStandingValue(standingList[i]);
                    }

                    newTeamRow["DivisionName"] = vo.DivisionName;
                    newTeamRow["LeagueName"] = vo.LeagueName;
                    newTeamRow["SportName"] = vo.SportName;
                    newTeamRow["LeagueAllowTeamJoin"] = vo.LeagueAllowTeamJoin;
                    newTeamRow["IsReplaced"] = vo.IsReplaced;
                    newTeamRow["LadderRank"] = vo.LadderRank;
                    newTeamRow["CreateDate"] = vo.CreateDate;

                    divSSTable.Rows.Add(newTeamRow);
                }

                // Build order by expression according to 4 standing order defined in the sport.
                divSSTable.DefaultView.Sort = _orderBy;

                divSSTable = divSSTable.DefaultView.ToTable();

                //IM-858 If someone is marked as not playing it is showing them at the top since their total time is 0, but they should be at the bottom of the standings.
                divSSTable = MoveNoResultTeamsInTheBottom(divSSTable, standingList);

                int totalTeams = divSSTable.Rows.Count;
                for (int i = 0; i < totalTeams; i++)
                {
                    DataRow sortedTeamRow = divSSTable.Rows[i];

                    if (this.DiscardOrderWithinTeamName)
                    {// no need order
                        sortedTeamRow["Name"] = sortedTeamRow["Name"];
                    }
                    else
                    {
                        sortedTeamRow["Name"] = (i + 1).ToString() + " " + sortedTeamRow["Name"];
                    }
                    // update team statistics vo
                    TeamStatisticsVO vo = (TeamStatisticsVO)sortedTeamRow["TeamVO"];
                    vo.TeamRank = i + 1;
                    vo.TotalTeams = totalTeams;
                }
            }

            return divSSTable;
        }

        /// <summary>
        /// Get team statistics without GB or else, no order is enforced.
        /// </summary>
        /// <returns></returns>
        public List<TeamStatisticsVO> GetTeamStatisticsList()
        {
            Dictionary<string, TeamStatisticsVO> voDict = GeEmptyTeamStatisticsDict();

            DataTable eventTeamTable = GetEventTeamTable();
            foreach (DataRow eventTeam in eventTeamTable.Rows)
            {
                UpdateTeamsStatistics(voDict, eventTeam);
            }

            List<TeamStatisticsVO> voList = new List<TeamStatisticsVO>();
            voList.AddRange(voDict.Values);

            CompleteTeamStatistics(voList);

            return voList;
        }

        public Dictionary<string, TeamStatisticsVO> GetTeamStatisticsDict()
        {
            Dictionary<string, TeamStatisticsVO> voDict = GeEmptyTeamStatisticsDict();

            DataTable gameTable = GetEventTeamTable();
            foreach (DataRow gameRow in gameTable.Rows)
            {
                UpdateTeamsStatistics(voDict, gameRow);
            }

            List<TeamStatisticsVO> voList = new List<TeamStatisticsVO>();
            voList.AddRange(voDict.Values);

            CompleteTeamStatistics(voList);

            return voDict;
        }

        /// <summary>
        /// Build a few parameters, which can be used by other methods.
        /// </summary>
        private void BuildStandingOrderBy()
        {
            // By default, all teams with 'Type' (-1) should be placed at the 
            // bottom of division standings.
            //_orderBy = "Type desc"; 
            // it seems that Type = -1 is not used any more so we don't use "Type desc"
            _orderBy = string.Empty;
            for (int i = 0; i < _standingOptions.Length; i++)
            {
                switch (_standingOptions[i])
                {
                    case StandingOptions.TOTAL:
                        _orderBy += (", " + StandingOptions.TOTAL.ToString() + (_sport.PointRanking ? " asc" : " desc"));
                        break;
                    case StandingOptions.AVERAGE:
                        _orderBy += (", " + StandingOptions.AVERAGE.ToString() + (_sport.PointRanking ? " asc" : " desc"));
                        break;
                    case StandingOptions.BEST_P:
                        _orderBy += (", " + StandingOptions.BEST_P.ToString() + " desc");
                        break;
                    case StandingOptions.WORST_P:
                        _orderBy += (", " + StandingOptions.WORST_P.ToString() + " asc");
                        break;
                }
            }

            if (string.IsNullOrEmpty(_orderBy))
            {
                _orderBy = "Name asc";
            }
            else
            {
                _orderBy += ", Name asc";
                _orderBy = _orderBy.Substring(2); // remove ", " two characters
            }


            // 1. First sort teams for "IsReplaced" which will be 1 for the teams got replaced 
            //    by forfeit or bye and 0 for the normal teams, so this first sort  will  send  all  those 
            //    teams to the bottom irrespective of there other standings
            //
            // 2. If ladder scheduling is enable then teams should be sorted for there rank first and not 
            //    for the other standings.
            _orderBy = ("IsReplaced asc, LadderRank asc,") + _orderBy;
        }

        private bool IsTwoTeamsTied(TeamStatisticsVO team1, TeamStatisticsVO team2)
        {
            for (int i = 0; i < _h2hNdx; i++)
            {
                if (!team1.GetStandingValue(_standingOptions[i]).Equals(team2.GetStandingValue(_standingOptions[i])))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
		private DataTable GetEventTeamTable()
		{
			string eventTeamQuery = string.Empty;

			switch ((int)_entityType)
			{
				case (int)Scope.Sport:
					{
						eventTeamQuery = "select T0.*, T1.Name as TeamName from SportEventTeams T0 left join Teams T1 on T1.ID = T0.TeamID left join Leagues T2 on T2.Id=T1.LeagueId where T1.SportId=@Id and T2.Active=1";
					}
					break;
				case (int)Scope.SportWithoutWaitlist:
					{
						eventTeamQuery = "select T0.*, T1.Name as TeamName from SportEventTeams T0 left join Teams T1 on T1.ID = T0.TeamID left join Leagues T2 on T2.Id=T1.LeagueId where T1.DivisionId<>'0' and T1.SportId=@Id and T2.Active=1";
					}
					break;
				case (int)Scope.League:
					{
						eventTeamQuery = "select T0.*, T1.Name as TeamName from SportEventTeams T0 left join Teams T1 on T1.ID = T0.TeamID where T1.LeagueId=@Id";
					}
					break;
				case (int)Scope.LeagueWithoutWaitlist:
					{
						eventTeamQuery = "select T0.*, T1.Name as TeamName from SportEventTeams T0 left join Teams T1 on T1.ID = T0.TeamID where T1.DivisionId<>'0' and T1.LeagueId=@Id";
					}
					break;
				case (int)Scope.Division:
					{
						eventTeamQuery = "select T0.*, T1.Name as TeamName from SportEventTeams T0 left join Teams T1 on T1.ID = T0.TeamID where T1.DivisionId=@Id";
					}
					break;
				case (int)Scope.Team:
					{
						eventTeamQuery = "select T0.*, T1.Name as TeamName from SportEventTeams T0 left join Teams T1 on T1.ID = T0.TeamID where T1.Id=@Id";
					}
					break;
				case (int)Scope.LeagueWaitlist: // league waitlist, division is "0"
					{
						eventTeamQuery = "select T0.*, T1.Name as TeamName from SportEventTeams T0 left join Teams T1 on T1.ID = T0.TeamID where T1.LeagueId=@Id and T1.DivisionId<>'0'";
					}
					break;
				case (int)Scope.Group:
					{
						eventTeamQuery = "select T0.*, T1.Name as TeamName from SportEventTeams T0 left join Teams T1 on T1.ID = T0.TeamID where T0.TeamID in (select U0.TeamId from GroupTeams U0 where U0.GroupId=@Id)";
					}
					break;
                case (int)Scope.SportEventTournament:
					{
                        eventTeamQuery = "select T0.*, T1.Name as TeamName from SportEventTeams T0 left join Teams T1 on T1.ID = T0.TeamID JOIN dbo.SportEvents T2 ON T2.ID = T0.EventID where T2.SportEventTournamentId=@Id";
                        if (BasedOnRound != 0) eventTeamQuery += " AND T2.Round=" + this.BasedOnRound;
                    }
					break;
				default:
					throw new Exception(Strings.EventTeamStandings_NotSupported);
			}

			List<SqlParameter> paras = new List<SqlParameter>();
			paras.Add(DBHelper.CreateVarCharSqlParameter("@Id", _entityID));

			DataTable dt = DbManager.GetDbContext(_sport.SchoolId).ExecuteQuery(eventTeamQuery.ToString(), paras.ToArray());

			return dt;
		}

		private Dictionary<string, TeamStatisticsVO> GeEmptyTeamStatisticsDict()
		{
			Dictionary<string, TeamStatisticsVO> voDict = new Dictionary<string, TeamStatisticsVO>();


			string teamCond = string.Empty;
			switch ((int)_entityType)
			{
				case (int)Scope.Sport:
					teamCond = "T0.LeagueId in (select U0.Id from Leagues U0 where U0.SportId=@Id)";
					break;
				case (int)Scope.SportWithoutWaitlist:
					teamCond = "T0.DivisionId<>'0' and T0.LeagueId in (select U0.Id from Leagues U0 where U0.SportId=@Id)";
					break;
				case (int)Scope.League:
					teamCond = "T0.LeagueId=@Id";
					break;
				case (int)Scope.LeagueWaitlist: // league waitlist, division is "0"
					teamCond = "T0.LeagueId=@Id and T0.DivisionId='0'";
					break;
				case (int)Scope.LeagueWithoutWaitlist:
					teamCond = "T0.LeagueId=@Id and T0.DivisionId<>'0'";
					break;
				case (int)Scope.Division:
					teamCond = "T0.DivisionId=@Id";
					break;
				case (int)Scope.Team:
					teamCond = "T0.Id=@Id";
					break;
				case (int)Scope.Group:
					teamCond = "T0.Id in (select U0.TeamId from GroupTeams U0 where U0.GroupId=@Id)";
					break;
                case (int)Scope.SportEventTournament:
                    string teamIds = "'" + string.Join("','", this.SportEventTournamentTeams.ToArray()) + "'";
                    teamCond = "T0.Id in (" + teamIds + ")";
					break;
				default:
					throw new Exception(Strings.EventTeamStandings_NotSupported);
			}
			//if turn RemainEligibleIfOnRoster on, we dont strike out member name that only suspend by the ineligible
			//The option should only for SSO schools
			bool needExceptIneligibleSuspension = _sport.SportSchool.NeedRemainEligibleIfOnRoster;
			string exceptIneligiblePart = string.Format(" and ReasonType <> {0} ", (int)Suspension.SuspensionReasonType.Ineligible);


			string teamNameWithStrike = string.Format(@"Name = case when((T0.ReplacedBy is not null) or exists (select * from Suspensions inner join Schools on Suspensions.SchoolId=Schools.SchoolId where SuspendedObjectId=T0.Id and SuspendedObjectType='1' {0} and (isnull(Suspensions.EndDate,0)<>0 and Suspensions.EndDate<(DATEADD(HH,Schools.TimeZone,Getdate())) and ((Suspensions.Type<>'0') or (Suspensions.Type='0' and Suspensions.EndDate>GETDATE())) ))) then (case when (T0.ReplacedBy is not null) then ('<strike>' + T0.Name + '</strike>') else ('<strike>' + T0.Name + '</strike>(Susp)') end ) else T0.Name end", needExceptIneligibleSuspension ? exceptIneligiblePart : string.Empty);

			string sqlCmd = "select T0.Id," + teamNameWithStrike + ",(Case when (T0.ReplacedBy is not null) then '1' else '0' end ) as IsReplaced, T0.DivisionId, T0.LeagueId, T0.SportId, T0.Type, (select Top 1 U0.[Rank] from HallsOfChampions U0 where U0.TeamId=T0.Id order by U0.[Rank] desc) as PlayoffBestRank, (case when (select top(1) L.LadderScheduling from Leagues L where L.Id=T0.LeagueId)='1' then isnull(T0.LadderRank,'0') else '0' end) as LadderRank, T0.ActiveTeam,(select LegName from Leagues where Id=T0.LeagueId) as LeagueName,(select SportName from Sports where Id=T0.SportId) as SportName, (select Name from Divisions where Id=T0.DivisionId) as DivisionName,(select AllowTeamJoin from Leagues where Id=T0.LeagueId) as LeagueAllowTeamJoin,T0.CreateDate from Teams T0 where " + teamCond;
			List<SqlParameter> paras = new List<SqlParameter>();
			paras.Add(new SqlParameter("@Id", _entityID));

			DataTable dt = DbManager.GetDbContext(_sport.SchoolId).ExecuteQuery(sqlCmd, paras.ToArray());
			foreach (DataRow row in dt.Rows)
			{
				TeamStatisticsVO vo = new TeamStatisticsVO();
				vo.TeamID = Convert.ToString(row[0]);
				vo.TeamName = Convert.ToString(row[1]);
				vo.IsReplaced = Convert.ToInt32(row[2]);
				vo.DivisionID = Convert.ToString(row[3]);
				vo.LeagueID = Convert.ToString(row[4]);
				vo.SportID = Convert.ToString(row[5]);
				vo.PlayoffBestRank = DBNull.Value.Equals(row[7]) ? 0 : (int)row[8];
				vo.LadderRank = Convert.ToInt32(row[8]);
				vo.ActiveTeam = Convert.ToBoolean(row[9]);
				vo.LeagueName = Convert.ToString(row[10]);
				vo.SportName = Convert.ToString(row[11]);
				vo.DivisionName = Convert.ToString(row[12]);
				vo.LeagueAllowTeamJoin = Convert.ToBoolean(row[13]);
				vo.CreateDate = Convert.ToDateTime(row[14]);
				voDict.Add(vo.TeamID, vo);
			}

			return voDict;
		}

        private static void UpdateTeamsStatistics(Dictionary<string, TeamStatisticsVO> voDict, DataRow eventTeamRow/* a played game */)
        {
            string teamID = eventTeamRow["TeamID"].ToString();
            TeamStatisticsVO teamVO = voDict.ContainsKey(teamID) ? voDict[teamID] : (new TeamStatisticsVO());
            if (string.IsNullOrEmpty(teamVO.TeamID))
            {
                return;
            }

            if(!(DBNull.Value.Equals(eventTeamRow["Result"])))
            {
                teamVO.ResultList.Add((double) eventTeamRow["Result"]);
                teamVO.PositionList.Add((int) eventTeamRow["Position"]);
            }
        }

        private void CompleteTeamStatistics(List<TeamStatisticsVO> voList)
        {
            if (voList.Count == 0)
            {
                return;
            }

            foreach (TeamStatisticsVO teamVO in voList)
            {
                teamVO.PositionList.Sort(delegate(int a, int b) { return a.CompareTo(b); });

                if (teamVO.PositionList.Count > 0)
                {
                    teamVO.BestPosition = teamVO.PositionList[0];
                    teamVO.WorstPosition = teamVO.PositionList[teamVO.PositionList.Count - 1];
                }

                int dropLowest = 0;
                int dropHighest = 0;

                // required in bug 1813 - event scheduling
                // we may need to drop event scores after the total number of events is larger than the total of drop lowest and drop highest
                if (teamVO.NumOfEvents > (_sport.DropLowest + _sport.DropHighest))
                {
                    dropLowest = _sport.DropLowest;
                    dropHighest = _sport.DropHighest;
                }

                int count = teamVO.NumOfEvents - dropLowest;

                if (_sport.UseActualResult)
                {
                    for (int i = dropHighest; i < count; i++)
                    {
                        teamVO.TotalEventPoints += teamVO.ResultList[i];
                    }
                }
                else
                {
                    for (int i = dropHighest; i < count; i++)
                    {
                        int position = teamVO.PositionList[i];
                        if (position > 0 && position <= this.AssignedPointValues.Length)
                        {
                            teamVO.TotalEventPoints += double.Parse(this.AssignedPointValues[position - 1]);
                        }
                        else
                        {
                            teamVO.TotalEventPoints += 0;
                        }
                    }
                }

                teamVO.AverageEventPoints = (teamVO.NumOfEvents - dropLowest - dropHighest) == 0 ? 0 : (teamVO.TotalEventPoints / (teamVO.NumOfEvents - dropLowest - dropHighest));
            }
        }

        //if the standing result is all 0 then not input result yet, we put team in the bottom of the standings
        public DataTable MoveNoResultTeamsInTheBottom(DataTable table, List<StandingOptions> standingList)
        {
            DataTable clone = table.Clone();
            //get all no result standing row then put in dictionary
            Dictionary<string, DataRow> noResultDics = new Dictionary<string, DataRow>();
            foreach (DataRow row in table.Rows)
            {
                bool markedPlay = false;
                for (int k = 0; k < standingList.Count; k++)
                {
                    if (!row[k + 4].ToString().Equals("0") && !row[k + 4].ToString().Equals("0.0"))
                    {
                        markedPlay = true;
                        break;
                    }
                }
                if (!markedPlay)
                {
                    noResultDics.Add(row["Id"].ToString(), row);
                }
            }

            //re-building the data table with the 0 result in the bottom of the data view
            foreach (DataRow row in table.Rows)
            {
                if (!noResultDics.ContainsKey(row["Id"].ToString()))
                {
                    clone.Rows.Add(row.ItemArray);
                }
            }
            foreach (var DicValue in noResultDics)
            {
                clone.Rows.Add(DicValue.Value.ItemArray);
            }

            return clone;
        }

        public DataTable OrderResultTeams(DataTable table, List<StandingOptions> standingList)
        {
            DataTable clone = table.Clone();
            table.DefaultView.Sort = _orderBy;
            clone = table.DefaultView.ToTable();
            clone = MoveNoResultTeamsInTheBottom(clone, standingList);
            return clone;
        }

        public class TeamStatisticsVO
        {
            private string _teamID;
            public string TeamID
            {
                get { return _teamID; }
                set { _teamID = value; }
            }

            private string _teamName;
            public string TeamName
            {
                get { return _teamName.Length > 18 ? (_teamName.Substring(0, 15) + "...") : _teamName; }
                set { _teamName = value; }
            }
            private DateTime _createDate;
            public DateTime CreateDate
            {
                get { return _createDate; }
                set { _createDate = value; }
            }

            // indicate if a team is approved
            public bool ActiveTeam { get; set; }

            public string HTMLTeamName
            {
                get { return Convert.ToBoolean(_isReplaced) ? ("<strike>" + TeamName + "</strike>") : TeamName; }
            }

            public string DivisionID;
            public string LeagueID;
            public string SportID;

            private bool? _isShowTime = null;
            public bool IsShowTime
            {
                get
                {
                    if (_isShowTime == null)
                    {
                        Sport sport = Sport.Create(this.SportID);
                        if (sport.ResultType == 0 && sport.UseActualResult)
                        {
                            _isShowTime = true;
                        }
                        else
                        {
                            _isShowTime = false;
                        }
                    }

                    return (bool) _isShowTime;
                }
            }

            private int _teamRank;
            public int TeamRank
            {
                get { return _teamRank; }
                set { _teamRank = value; }
            }

            // ---------------------------------------------
            // for event sports
            public int NumOfEvents
            {
                get
                {
                    return this.PositionList.Count;
                }
            }

            private List<double> _resultList = new List<double>();
            public List<double> ResultList
            {
                get { return _resultList; }
            }

            private List<int> _positionList = new List<int>();
            public List<int> PositionList
            {
                get { return _positionList; }
            }

            public double TotalEventPoints { get; set; }
            public double AverageEventPoints { get; set; }

            public int BestPosition { get; set; }
            public int WorstPosition { get; set; }

            // ---------------------------------------------------

            private int _totalTeams;
            public int TotalTeams
            {
                get { return _totalTeams; }
                set { _totalTeams = value; }
            }

            private int _isReplaced;
            public int IsReplaced
            {
                get { return _isReplaced; }
                set { _isReplaced = value; }
            }

            private int _LadderRank;
            public int LadderRank
            {
                get { return _LadderRank; }
                set { _LadderRank = value; }
            }

            public string DivisionName { get; set; }
            public string LeagueName { get; set; }
            public string SportName { get; set; }
            public bool LeagueAllowTeamJoin { get; set; }

            private int _playoffBestRank;
            public int PlayoffBestRank
            {
                get { return _playoffBestRank; }
                set { _playoffBestRank = value; }
            }

            public double GetStandingValue(StandingOptions option)
            {
                switch (option)
                {
                    case StandingOptions.TOTAL:
                        {
                            //if (this.IsShowTime)
                            //{
                            //    int hours = (int) (this.TotalEventPoints / (60 * 60));
                            //    int minutes = (int)((this.TotalEventPoints % (60 * 60)) / 60);
                            //    int seconds = (int)(this.TotalEventPoints % 60);

                            //    StringBuilder sbTime = new StringBuilder();
                            //    if (hours > 0) sbTime.Append(hours).Append('h');
                            //    if (minutes > 0) sbTime.Append(minutes).Append('m');
                            //    if (seconds > 0) sbTime.Append(seconds).Append('s');

                            //    return sbTime.ToString();
                            //}
                            //else 
                                return this.TotalEventPoints;
                        }
                    case StandingOptions.AVERAGE:
                        {
                            //if (this.IsShowTime)
                            //{
                            //    int hours = (int)(this.AverageEventPoints / (60 * 60));
                            //    int minutes = (int)((this.AverageEventPoints % (60 * 60)) / 60);
                            //    int seconds = (int)(this.AverageEventPoints % 60);

                            //    StringBuilder sbTime = new StringBuilder();
                            //    if (hours > 0) sbTime.Append(hours).Append('h');
                            //    if (minutes > 0) sbTime.Append(minutes).Append('m');
                            //    if (seconds > 0) sbTime.Append(seconds).Append('s');

                            //    return sbTime.ToString();
                            //}
                            //else 
                                return Math.Round(this.AverageEventPoints, 2);
                        }
                    case StandingOptions.BEST_P:
                        return this.BestPosition;
                    case StandingOptions.WORST_P:
                        return this.WorstPosition;
                    default:
                        throw new Exception(Strings.EventTeamStandings_NotSupported);
                }
            }

            public string GetStandingDisplayValue(StandingOptions option)
            {
                switch (option)
                {
                    case StandingOptions.TOTAL:
                        {
                            if (this.IsShowTime)
                            {
                                int hours = (int)(this.TotalEventPoints / (60 * 60));
                                int minutes = (int)((this.TotalEventPoints % (60 * 60)) / 60);
                                int seconds = (int)(this.TotalEventPoints % 60);

                                StringBuilder sbTime = new StringBuilder();
                                if (hours > 0) sbTime.Append(hours).Append('h');
                                if (minutes > 0) sbTime.Append(minutes).Append('m');
                                if (seconds > 0) sbTime.Append(seconds).Append('s');

                                return sbTime.ToString();
                            }
                            else
                                return this.TotalEventPoints.ToString();
                        }
                    case StandingOptions.AVERAGE:
                        {
                            if (this.IsShowTime)
                            {
                                int hours = (int)(this.AverageEventPoints / (60 * 60));
                                int minutes = (int)((this.AverageEventPoints % (60 * 60)) / 60);
                                int seconds = (int)(this.AverageEventPoints % 60);

                                StringBuilder sbTime = new StringBuilder();
                                if (hours > 0) sbTime.Append(hours).Append('h');
                                if (minutes > 0) sbTime.Append(minutes).Append('m');
                                if (seconds > 0) sbTime.Append(seconds).Append('s');

                                return sbTime.ToString();
                            }
                            else
                                return Math.Round(this.AverageEventPoints, 2).ToString();
                        }
                    case StandingOptions.BEST_P:
                        return this.BestPosition.ToString();
                    case StandingOptions.WORST_P:
                        return this.WorstPosition.ToString();
                    default:
                        throw new Exception(Strings.EventTeamStandings_NotSupported);
                }
            }
        }
    }
}

