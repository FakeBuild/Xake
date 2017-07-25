namespace Xake

[<AutoOpen>]
module XakeScript =

    /// Creates the rule for specified file pattern.
    let ( ..?> ) fn fnRule = FileConditionRule (fn, fnRule)

    let ( ..> ) pattern actionBody = FileRule (pattern, actionBody)

    let ( *..> ) (patterns: #seq<string>) actionBody =
        MultiFileRule (patterns |> List.ofSeq, actionBody)

    /// Creates phony action (check if I can unify the operator name)
    let (=>) name action = PhonyRule (name, action)

    /// Main type.
    type XakeScript = XakeScript of ExecOptions * Rules<ExecContext>

    /// Script builder.
    type RulesBuilder(options) =

        let updRules (XakeScript (options,rules)) f = XakeScript (options, f(rules))
        let updTargets (XakeScript (options,rules)) f = XakeScript ({options with Targets = f(options.Targets)}, rules)
        let addRule rule (Rules rules) :Rules<_> =    Rules (rule :: rules)

        let updateVar (key: string) (value: string) =
            List.filter(fst >> ((<>) key)) >> ((@) [key, value])

        member o.Bind(x,f) = f x
        member o.Zero() = XakeScript (options, Rules [])
        member o.Yield(())    = o.Zero()

        member this.Run(XakeScript (options,rules)) =
            ExecCore.runScript options rules

        [<CustomOperation("dryrun")>] member this.DryRun(XakeScript (options, rules))
            = XakeScript ({options with DryRun = true}, rules)

        [<CustomOperation("var")>] member this.AddVar(XakeScript (options, rules), name, value)
            =
            XakeScript ({options with Vars = options.Vars |> updateVar name value }, rules)

        [<CustomOperation("filelog")>] member this.FileLog(XakeScript (options, rules), filename, ?loglevel)
            =
            let loglevel = defaultArg loglevel Verbosity.Chatty in
            XakeScript ({options with FileLog = filename; FileLogLevel = loglevel}, rules)

        [<CustomOperation("consolelog")>] member this.ConLog(XakeScript (options, rules), ?loglevel)
            =
            let loglevel = defaultArg loglevel Verbosity.Chatty in
            XakeScript ({options with ConLogLevel =loglevel}, rules)

        [<CustomOperation("rule")>] member this.Rule(script, rule)
            = updRules script (addRule rule)
        // [<CustomOperation("addRule")>] member this.AddRule(script, pattern, action)
        //     = updRules script (pattern *> action |> addRule)
        [<CustomOperation("phony")>] member this.Phony(script, name, action)
            = updRules script (name => action |> addRule)
        [<CustomOperation("rules")>] member this.Rules(script, rules: #seq<ExecContext Rule>)
            = (rules |> Seq.map addRule |> Seq.fold (>>) id) |> updRules script

        [<CustomOperation("want")>] member this.Want(script, targets)
            = updTargets script (function |[] -> targets |x -> x)    // Options override script!
        [<CustomOperation("wantOverride")>] member this.WantOverride(script,targets)
            = updTargets script (fun _ -> targets)
