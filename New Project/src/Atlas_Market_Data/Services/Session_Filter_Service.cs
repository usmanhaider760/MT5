using Atlas_Domain.Enums;
using Atlas_Shared;

namespace Atlas_Market_Data.Services;

public class Session_Filter_Service
{
    public Session_Type Get_Current_Session(DateTime utc_now)
    {
        var time = TimeOnly.FromTimeSpan(utc_now.TimeOfDay);

        bool in_london = time >= Trading_Constants.London_Open && time < Trading_Constants.London_Close;
        bool in_ny     = time >= Trading_Constants.NY_Open     && time < Trading_Constants.NY_Close;
        bool in_asian  = time >= Trading_Constants.Asian_Open  || time < Trading_Constants.Asian_Close;

        if (in_london && in_ny) return Session_Type.London_NY_Overlap;
        if (in_london)          return Session_Type.London;
        if (in_ny)              return Session_Type.New_York;
        if (in_asian)           return Session_Type.Asian;
        return Session_Type.Off_Hours;
    }

    public int Get_Session_Quality_Score(Session_Type session) => session switch
    {
        Session_Type.London_NY_Overlap => 15,
        Session_Type.London            => 13,
        Session_Type.New_York          => 12,
        Session_Type.Asian             => 7,
        _                              => 0
    };

    public bool Is_Trading_Session_Active(Session_Type session) =>
        session != Session_Type.Off_Hours;

    public bool Is_Good_For_Breakout(Session_Type session) =>
        session == Session_Type.London || session == Session_Type.London_NY_Overlap;
}
