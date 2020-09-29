using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Data.Entity;

namespace SWW.GStats.Common
{
    public partial class AccessData
    {
        public static string nameDB { get; private set; }
        public AccessData(string nameDataBase)
        {
            nameDB = nameDataBase;
        }
        public static int PutServerInfo(string endpoint, string requestBody)
        {
            int statusCode;
            ServerInfo requestObject = JsonConvert.DeserializeObject<ServerInfo>(requestBody);
            try
            {
                using (var db = new ContextDB(nameDB))
                {
                    Server server = db.Servers.FirstOrDefault(s => s.Endpoint == endpoint);
                    if (server == null)
                    {
                        server = new Server { Endpoint = endpoint, Name = requestObject.name };
                        db.Servers.Add(server);
                        foreach (var gm in requestObject.gameModes)
                        {
                            db.GameModes.Add(new GameMode
                            { Server = server, Name = gm });
                        }
                    }
                    else
                    {
                        server.Name = requestObject.name;
                        var removedGameModes = db.GameModes.Where(gm => gm.ServerId == server.Id);
                        db.GameModes.RemoveRange(removedGameModes);
                        foreach (var gm in requestObject.gameModes)
                        {
                            db.GameModes.Add(new GameMode
                            { Server = server, Name = gm });
                        }
                    }
                    db.SaveChanges();
                }
                statusCode = 200;
            }
            catch
            {
                statusCode = 400;
            }
            return statusCode;
        }
        public static int PutMatchInfo(string endpoint, string matchTimestamp, string requestBody)
        {
            int statusCode;
            MatchInfo requestObject = JsonConvert.DeserializeObject<MatchInfo>(requestBody);
            try
            {
                using (var db = new ContextDB(nameDB))
                {
                    Server server = db.Servers.Include(s => s.Matches).FirstOrDefault(s => s.Endpoint == endpoint);
                    if (server == null)
                        return 400;
                    DateTime timestamp = DateTime.Parse(matchTimestamp);
                    Match match = server.Matches.FirstOrDefault(m => m.Timestamp == timestamp);
                    if (match == null)
                    {
                        match = new Match
                        {
                            Timestamp = timestamp,
                            Map = requestObject.map,
                            GameMode = requestObject.gameMode,
                            FragLimit = requestObject.fragLimit,
                            TimeLimit = requestObject.timeLimit,
                            TimeElapsed = requestObject.timeElapsed,
                            Server = server
                        };
                        db.Matches.Add(match);
                        int i = 1;
                        foreach (var scbrd in requestObject.scoreboard)
                        {
                            db.Scoreboards.Add(new Scoreboard
                            {
                                PlaceInMatch = i,
                                Name = scbrd.name,
                                Frags = scbrd.frags,
                                Kills = scbrd.kills,
                                Deaths = scbrd.deaths,
                                Match = match
                            });
                            i++;
                        }
                    }
                    else
                    {
                        match.Timestamp = timestamp;
                        match.Map = requestObject.map;
                        match.GameMode = requestObject.gameMode;
                        match.FragLimit = requestObject.fragLimit;
                        match.TimeLimit = requestObject.timeLimit;
                        match.TimeElapsed = requestObject.timeElapsed;
                        var removeScoreboards = db.Scoreboards.Where(scr => scr.MatchId == match.Id);
                        db.Scoreboards.RemoveRange(removeScoreboards);
                        int i = 1;
                        foreach (var scbrd in requestObject.scoreboard)
                        {
                            db.Scoreboards.Add(new Scoreboard
                            {
                                PlaceInMatch = i,
                                Name = scbrd.name,
                                Frags = scbrd.frags,
                                Kills = scbrd.kills,
                                Deaths = scbrd.deaths,
                                Match = match
                            });
                            i++;
                        }
                    }
                    db.SaveChanges();
                    statusCode = 200;
                }
            }
            catch
            {
                statusCode = 400;
            }
            return statusCode;
        }
        public static string GetAllServersInfo()
        {
            try
            {
                using (var db = new ContextDB(nameDB))
                {
                    var serverInfo = db.Servers.Select(s => new
                    {
                        endpoint = s.Endpoint,
                        info = new
                        {
                            name = s.Name,
                            gameModes = db.GameModes.Where(gm => gm.ServerId == s.Id).Select(x => x.Name).ToList()
                        }
                    });
                    var json = JsonConvert.SerializeObject(serverInfo, Formatting.Indented);
                    return json;
                }
            }
            catch
            {
                return string.Empty;
            }
        }
        public static string GetServerInfo(string endpoint)
        {
            try
            {
                using (var db = new ContextDB(nameDB))
                {
                    var serverInfo = db.Servers.Where(s => s.Endpoint == endpoint).Select(s => new
                    {
                        name = s.Name,
                        gameModes = db.GameModes.Where(gm => gm.ServerId == s.Id).Select(x => x.Name).ToList()
                    }).First();
                    return JsonConvert.SerializeObject(serverInfo, Formatting.Indented);
                }
            }
            catch
            {
                return string.Empty;
            }
        }
        public static string GetMatchInfo(string endpoint, string timestamp)
        {
            try
            {
                using (var db = new ContextDB(nameDB))
                {
                    var matchInfo = db.Servers.Include(s => s.Matches)
                        .First(s => s.Endpoint == endpoint)
                        .Matches.Where(m => m.Timestamp == DateTime.Parse(timestamp))
                        .Select(m => new
                        {
                            map = m.Map,
                            gameMode = m.GameMode,
                            fragLimit = m.FragLimit,
                            timeLimit = m.TimeLimit,
                            timeElapsed = m.TimeElapsed,
                            scoreboard = db.Scoreboards.Where(sb => sb.MatchId == m.Id).Select(sb => new
                            {
                                name = sb.Name,
                                frags = sb.Frags,
                                kills = sb.Kills,
                                deaths = sb.Deaths
                            })
                        }).First();
                    return JsonConvert.SerializeObject(matchInfo, Formatting.Indented);
                }
            }
            catch
            {
                return string.Empty;
            }
        }
        public static string RecentMatches(int count)
        {
            try
            {
                using (var db = new ContextDB(nameDB))
                {
                    var matches = db.Matches.OrderByDescending(m => m.Timestamp).Take(count)
                        .Select(m => new
                        {
                            server = m.Server.Endpoint,
                            timestamp = m.Timestamp,
                            results = new MatchInfo
                            {
                                map = m.Map,
                                gameMode = m.GameMode,
                                fragLimit = m.FragLimit,
                                timeLimit = m.TimeLimit,
                                timeElapsed = m.TimeElapsed,
                                scoreboard = m.Scoreboards.Select(sc => new Scboard
                                {
                                    name = sc.Name,
                                    frags = sc.Frags,
                                    kills = sc.Kills,
                                    deaths = sc.Deaths
                                }).ToList()
                            }
                        }).ToList();
                    var recentMatches = matches
                        .Select(m => new RecentMatch
                        {
                            server = m.server,
                            timestamp = m.timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            results = m.results
                        });

                    return JsonConvert.SerializeObject(recentMatches, Formatting.Indented);
                }
            }
            catch
            {
                return string.Empty;
            }
        }
        public static string BestPlayers(int count)
        {
            try
            {
                using (var db = new ContextDB(nameDB))
                {
                    var scoreboards = db.Scoreboards.GroupBy(sc => sc.Name.ToLower())
                        .Select(g => new
                        {
                            name = g.Key,
                            allKills = db.Scoreboards.Where(sc => sc.Name.ToLower() == g.Key)
                                .Sum(sc => sc.Kills),
                            allDeaths = db.Scoreboards.Where(sc => sc.Name.ToLower() == g.Key)
                                .Sum(sc => sc.Deaths),
                            count = g.Count()
                        }).Where(pl => pl.allDeaths != 0 && pl.count >= 10)
                        .Select(pl => new BestPlayer
                        {
                            name = pl.name,
                            killToDeathRatio = (double)pl.allKills / pl.allDeaths
                        })
                        .OrderByDescending(g => g.killToDeathRatio).Take(count);
                    return JsonConvert.SerializeObject(scoreboards, Formatting.Indented);
                }
            }
            catch
            {
                return string.Empty;
            }
        }
        public static string PopularServers(int count)
        {
            try
            {
                using (var db = new ContextDB(nameDB))
                {
                    DateTime MaxDayOnStatServer = db.Matches.Max(m => DbFunctions.TruncateTime(m.Timestamp).Value);
                    var popularServerList = db.Servers.Include(s => s.Matches).Select(s => new
                    {
                        endpoint = s.Endpoint,
                        name = s.Name,
                        matchesCount = s.Matches.Count(),
                        firstDay = db.Matches.Where(m => s.Id == m.ServerId).Min(m => DbFunctions.TruncateTime(m.Timestamp).Value)
                    }).ToList();

                    var popularServers = popularServerList.Select(ps => new PopularServer
                    {
                        endpoint = ps.endpoint,
                        name = ps.name,
                        averageMatchesPerDay = (double)ps.matchesCount / ((MaxDayOnStatServer - ps.firstDay).Days + 1)
                    })
                    .OrderByDescending(s => s.averageMatchesPerDay).Take(count);

                    return JsonConvert.SerializeObject(popularServers, Formatting.Indented);
                }
            }
            catch
            {
                return string.Empty;
            }
        }
        public static string GetServerStats(string endpoint)
        {
            ServerStats serverStats = new ServerStats();

            using (var db = new ContextDB(nameDB))
            {
                var server = db.Servers.FirstOrDefault(s => s.Endpoint == endpoint);
                if (server == null)
                    return string.Empty;
                try
                {
                    var matches = db.Matches.Include(m => m.Scoreboards).Where(m => m.ServerId == server.Id);
                    if (matches.Count() == 0)
                        return JsonConvert.SerializeObject(serverStats, Formatting.Indented);

                    var MatchesPerDay = matches
                        .Select(m => new { day = DbFunctions.TruncateTime(m.Timestamp) })
                        .GroupBy(g => g.day)
                        .Select(g => new { Date = g.Key, Count = g.Count() });

                    serverStats.totalMatchesPlayed = matches.Count();
                    serverStats.maximumMatchesPerDay = MatchesPerDay.Max(m => m.Count);

                    DateTime MaxDayOnStatServer = db.Matches.Max(m => DbFunctions.TruncateTime(m.Timestamp).Value);
                    DateTime MinDayOnServer = matches.Min(m => DbFunctions.TruncateTime(m.Timestamp).Value);
                    int countDayOnStatServer = (MaxDayOnStatServer - MinDayOnServer).Days + 1;
                    serverStats.averageMatchesPerDay = (double)serverStats.totalMatchesPlayed / countDayOnStatServer;

                    serverStats.maximumPopulation = matches.Select(m => m.Scoreboards.Count()).ToList().Max();
                    serverStats.averagePopulation = matches.Select(m => m.Scoreboards.Count()).ToList().Sum() / (double)matches.Count();
                    serverStats.top5GameModes = matches.GroupBy(m => m.GameMode)
                        .Select(m => new { gameMode = m.Key, count = m.Count() })
                        .OrderByDescending(g => g.count).Take(5)
                        .Select(g => g.gameMode).ToList();
                    serverStats.top5Maps = matches.GroupBy(m => m.Map)
                        .Select(g => new { map = g.Key, count = g.Count() })
                        .OrderByDescending(g => g.count).Take(5)
                        .Select(g => g.map).ToList();
                }
                catch
                {
                    return string.Empty;
                }
            }
            return JsonConvert.SerializeObject(serverStats, Formatting.Indented);
        }
        public static string GetPlayerStats(string name)
        {
            PlayerStats playerStats = new PlayerStats();
            using (var db = new ContextDB(nameDB))
            {
                name = name.ToLower();
                try
                {
                    var scoreboards = db.Scoreboards.Where(sc => sc.Name.ToLower() == name)
                    .Include(sc => sc.Match);
                    var matches = scoreboards.Select(scr => scr.Match);

                    DateTime MaxDayOnStatServer = db.Matches.Max(m => DbFunctions.TruncateTime(m.Timestamp).Value);
                    DateTime MinDayPlayerPlay = scoreboards.Min(sc => DbFunctions.TruncateTime(sc.Match.Timestamp).Value);
                    int countDayOnStatServer = (MaxDayOnStatServer - MinDayPlayerPlay).Days + 1;

                    playerStats.totalMatchesPlayed = scoreboards.Count();
                    playerStats.totalMatchesWon = scoreboards.Where(sc => sc.Name.ToLower() == name && sc.PlaceInMatch == 1).Count();

                    var servers = matches.GroupBy(m => m.Server.Endpoint)
                        .Select(g => new { server = g.Key, count = g.Count() })
                        .OrderByDescending(g => g.count);
                    playerStats.favoriteServer = servers.First().server;
                    playerStats.uniqueServers = servers.Count();
                    playerStats.favoriteGameMode = matches.GroupBy(m => m.GameMode)
                        .Select(g => new { gameMode = g.Key, count = g.Count() })
                        .OrderByDescending(g => g.count).First().gameMode;

                    var playersResult = scoreboards.Select(sc => new
                    {
                        place = sc.PlaceInMatch,
                        countPlayers = sc.Match.Scoreboards.Count
                    }).ToList();

                    playerStats.averageScoreboardPercent = playersResult.Select(pl => new {
                        percentScoreboard = (pl.countPlayers != 1) ?
                    ((pl.countPlayers - pl.place) / ((double)pl.countPlayers - 1)) * 100 : 100
                    })
                    .Average(x => x.percentScoreboard);
                    var MatchesPerDay = matches
                            .Select(m => new { day = DbFunctions.TruncateTime(m.Timestamp) })
                            .GroupBy(g => g.day)
                            .Select(g => new { Date = g.Key, Count = g.Count() });
                    playerStats.maximumMatchesPerDay = MatchesPerDay.Max(m => m.Count);
                    playerStats.averageMatchesPerDay = (double)playerStats.totalMatchesPlayed / countDayOnStatServer;
                    DateTime lastMatch = matches.Max(m => m.Timestamp);
                    playerStats.lastMatchPlayed = lastMatch.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    int totalKills = scoreboards.Sum(sc => sc.Kills);
                    int totalDeaths = scoreboards.Sum(sc => sc.Deaths);
                    playerStats.killToDeathRatio = (double)totalKills / totalDeaths;
                }
                catch
                {
                    return string.Empty;
                }
            }
            return JsonConvert.SerializeObject(playerStats, Formatting.Indented);
        }
       
    }
    //=====================================
    public class ContextDB : DbContext
    {
        public ContextDB(string conStrName) : base(conStrName) { }

        public DbSet<Server> Servers { get; set; }
        public DbSet<Match> Matches { get; set; }
        public DbSet<Scoreboard> Scoreboards { get; set; }
        public DbSet<GameMode> GameModes { get; set; }
    }
    //=====================================
    public class MatchInfo
    {
        public string map { get; set; }
        public string gameMode { get; set; }
        public int fragLimit { get; set; }
        public int timeLimit { get; set; }
        double TimeElapsed;
        public double timeElapsed
        {
            get { return Math.Round(TimeElapsed, 6); }
            set { TimeElapsed = value; }
        }
        public List<Scboard> scoreboard { get; set; }
    }
    public class ServerInfo
    {
        public string name { get; set; }
        public List<string> gameModes { get; set; }
    }
    public class ServerInfoFromAllServers
    {
        public string endpoint { get; set; }
        public ServerInfo info { get; set; }
    }
    //=====================
    public class GameMode
    {
        public int Id { get; set; }

        public int ServerId { get; set; }

        public Server Server { get; set; }
        public string Name { get; set; }
    }
    public class Match
    {
        public int Id { get; set; }
        public int ServerId { get; set; }
        public Server Server { get; set; }
        public DateTime Timestamp { get; set; }
        public string Map { get; set; }
        public string GameMode { get; set; }
        public int FragLimit { get; set; }
        public int TimeLimit { get; set; }
        double timeElapsed;
        public double TimeElapsed
        {
            get { return Math.Round(timeElapsed, 6); }
            set { timeElapsed = value; }
        }
        public ICollection<Scoreboard> Scoreboards { get; set; }
        public Match()
        {
            Scoreboards = new List<Scoreboard>();
        }
    }
    public class Scoreboard
    {
        public int Id { get; set; }
        public int PlaceInMatch { get; set; }
        public int MatchId { get; set; }
        public Match Match { get; set; }
        public string Name { get; set; }
        public int Frags { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
    }
    public class Server
    {
        public int Id { get; set; }
        public string Endpoint { get; set; }
        public string Name { get; set; }
        public ICollection<GameMode> GameModes { get; set; }
        public ICollection<Match> Matches { get; set; }
        public Server()
        {
            Matches = new List<Match>();
            GameModes = new List<GameMode>();
        }
    }
    //====================
    public class BestPlayer
    {
        public string name { get; set; }

        double killtoDeath;
        public double killToDeathRatio
        {
            get { return Math.Round(killtoDeath, 6); }
            set { killtoDeath = value; }
        }
    }
    public class PopularServer
    {
        public string endpoint { get; set; }
        public string name { get; set; }

        double AverageMatchesPerDay;
        public double averageMatchesPerDay
        {
            get { return Math.Round(AverageMatchesPerDay, 6); }
            set { AverageMatchesPerDay = value; }
        }
    }
    public class RecentMatch
    {
        public string server { get; set; }
        public string timestamp { get; set; }
        public MatchInfo results { get; set; }

    }
   //===============================
    public class PlayerStats
    {
        public int totalMatchesPlayed { get; set; }
        public int totalMatchesWon { get; set; }
        public string favoriteServer { get; set; }
        public int uniqueServers { get; set; }
        public string favoriteGameMode { get; set; }
        double AverageScoreboardPercent;
        public double averageScoreboardPercent
        {
            get { return Math.Round(AverageScoreboardPercent, 6); }
            set { AverageScoreboardPercent = value; }
        }
        public int maximumMatchesPerDay { get; set; }
        double AverageMatchesPerDay;
        public double averageMatchesPerDay
        {
            get { return Math.Round(AverageMatchesPerDay, 6); }
            set { AverageMatchesPerDay = value; }
        }
        public string lastMatchPlayed { get; set; }
        double KillToDeathRatio;
        public double killToDeathRatio
        {
            get { return Math.Round(KillToDeathRatio, 6); }
            set { KillToDeathRatio = value; }
        }
    }
    /// <summary>
    /// Start srv
    /// </summary>
    public class ServerStats
    {
        public ServerStats()
        {
            top5GameModes = new List<string>();
            top5Maps = new List<string>();
            parameterTest = 1;
        }
        public int parameterTest = 0;  
        public int totalMatchesPlayed { get; set; }
        public int maximumMatchesPerDay { get; set; }
        double AverageMatchesPerDay;
        public double averageMatchesPerDay
        {
            get { return Math.Round(AverageMatchesPerDay, 6); }
            set { AverageMatchesPerDay = value; }
        }

        public int maximumPopulation { get; set; }
        double AveragePopulation;
        public double averagePopulation
        {
            get { return Math.Round(AveragePopulation, 6); }
            set { AveragePopulation = value; }
        }
        public List<string> top5GameModes { get; set; }
        public List<string> top5Maps { get; set; }
    }
    //==============================
    public class Scboard
    {
        public string name { get; set; }
        public int frags { get; set; }
        public int kills { get; set; }
        public int deaths { get; set; }
    }
    //===============================================================================================

}


