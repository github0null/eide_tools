
////////////////////////////////////////////

int CODE_ERR = 1;
int CODE_DONE = 0;

////////////////////////////////////////////

try
{
    return main(args);
}
catch (Exception err)
{
    Console.WriteLine(err.Message);
    return CODE_ERR;
}

///////////////////////////////////////////

int main(string[] args)
{
    string[] helpTxt = {
        "params format error !",
        "",
        "format:",
        "\tverchk <lt|gt|eq|lte|gte> ver_str1 ver_str2 [errMsg]",
        "",
        "example:",
        "\tverchk lte 1.10.2 1.10.3 \"version must less than 1.10.3\""
    };

    var err = new Exception(string.Join('\n', helpTxt));

    if (args.Length < 3) throw err;

    Func<string, string, int> versionCompare = (string v1, string v2) => {

        var v1_li = v1.Split('.').Where(s => s.Trim() != string.Empty).ToArray();
        var v2_li = v2.Split('.').Where(s => s.Trim() != string.Empty).ToArray();

        // compare per number
        var minLen = Math.Min(v1_li.Length, v2_li.Length);
        for (int index = 0; index < minLen; index++)
        {
            try
            {
                var v_1 = int.Parse(v1_li[index]);
                var v_2 = int.Parse(v2_li[index]);
                if (v_1 > v_2) return 1;
                if (v_1 < v_2) return -1;
            }
            catch (FormatException)
            {
                throw new Exception("'ver_str1' or 'ver_str2' format error !, It must be a version string !");
            }
        }

        // if prefix is equal, compare len
        if (v1_li.Length > v2_li.Length) return 1;
        if (v1_li.Length < v2_li.Length) return -1;

        return 0;
    };

    int eCode = CODE_DONE;

    switch (args[0])
    {
        case "lt":
            eCode = versionCompare(args[1], args[2]) < 0 ? CODE_DONE : CODE_ERR;
            break;
        case "lte":
            eCode = versionCompare(args[1], args[2]) <= 0 ? CODE_DONE : CODE_ERR;
            break;
        case "gt":
            eCode = versionCompare(args[1], args[2]) > 0 ? CODE_DONE : CODE_ERR;
            break;
        case "gte":
            eCode = versionCompare(args[1], args[2]) >= 0 ? CODE_DONE : CODE_ERR;
            break;
        case "eq":
            eCode = args[1] == args[2] ? CODE_DONE : CODE_ERR;
            break;
        default:
            throw err;
    }

    string msgOut = string.Empty;

    var getFullCpName = (string cpId) => {
        return cpId switch {
            "lt" => "less than",
            "lte" => "less than or equal to",
            "gt" => "greater than",
            "gte" => "greater than or equal to",
            "eq" => "equal",
            _ => cpId,
        };
    };

    if (eCode != CODE_DONE)
        msgOut = args.Length > 3 ?
            args[3] : string.Format("check failed, version '{1}' is not {0} '{2}'", getFullCpName(args[0]), args[1], args[2]);
    else
        msgOut = "check passed !";

    if (!string.IsNullOrEmpty(msgOut))
    {
        Console.WriteLine(msgOut);
    }

    return eCode;
}
