# Branches and ranks. Ported from SierraBay12's maps/torch/torch_ranks.dm.

## Branches

ember-branch-corps-name = Expeditionary Corps
ember-branch-corps-short = SCGEC
ember-branch-fleet-name = Fleet
ember-branch-fleet-short = SCGF
ember-branch-scg-government-name = Sol Central Government
ember-branch-scg-government-short = SCG
ember-branch-civilian-name = Civilian
ember-branch-civilian-short = Civ

## Fleet, enlisted and non-commissioned

ember-rank-fleet-e1-name = Crewman Recruit
ember-rank-fleet-e1-short = CR
ember-rank-fleet-e2-name = Crewman Apprentice
ember-rank-fleet-e2-short = CA
ember-rank-fleet-e3-name = Crewman
ember-rank-fleet-e3-short = CN
ember-rank-fleet-e4-name = Petty Officer Third Class
ember-rank-fleet-e4-short = PO3
ember-rank-fleet-e5-name = Petty Officer Second Class
ember-rank-fleet-e5-short = PO2
ember-rank-fleet-e6-name = Petty Officer First Class
ember-rank-fleet-e6-short = PO1
ember-rank-fleet-e7-name = Chief Petty Officer
ember-rank-fleet-e7-short = CPO
ember-rank-fleet-e8-name = Senior Chief Petty Officer
ember-rank-fleet-e8-short = SCPO
ember-rank-fleet-e9-name = Master Chief Petty Officer
ember-rank-fleet-e9-short = MCPO
ember-rank-fleet-e9-command-name = Command Master Chief Petty Officer
ember-rank-fleet-e9-command-short = CMCPO
ember-rank-fleet-e9-fleet-name = Fleet Master Chief Petty Officer
ember-rank-fleet-e9-fleet-short = FLTCM
ember-rank-fleet-e9-force-name = Force Master Chief Petty Officer
ember-rank-fleet-e9-force-short = FORCM
ember-rank-fleet-e9-of-the-fleet-name = Master Chief Petty Officer of the Fleet
ember-rank-fleet-e9-of-the-fleet-short = MCPOF

## Fleet, commissioned

ember-rank-fleet-o1-name = Ensign
ember-rank-fleet-o1-short = ENS
ember-rank-fleet-o2-name = Sub-Lieutenant
ember-rank-fleet-o2-short = SLT
ember-rank-fleet-o3-name = Lieutenant
ember-rank-fleet-o3-short = LT
ember-rank-fleet-o4-name = Lieutenant Commander
ember-rank-fleet-o4-short = LCDR
ember-rank-fleet-o5-name = Commander
ember-rank-fleet-o5-short = CDR
ember-rank-fleet-o6-name = Captain
ember-rank-fleet-o6-short = CAPT
ember-rank-fleet-o7-name = Commodore
ember-rank-fleet-o7-short = CDRE
ember-rank-fleet-o8-name = Rear Admiral
ember-rank-fleet-o8-short = RADM
ember-rank-fleet-o9-name = Vice Admiral
ember-rank-fleet-o9-short = VADM
ember-rank-fleet-o10-name = Admiral
ember-rank-fleet-o10-short = ADM
ember-rank-fleet-o10-admiral-name = Fleet Admiral
ember-rank-fleet-o10-admiral-short = FADM

## Expeditionary Corps

ember-rank-corps-e1-name = Apprentice Explorer
ember-rank-corps-e1-short = AXPL
ember-rank-corps-e3-name = Explorer
ember-rank-corps-e3-short = XPL
ember-rank-corps-e5-name = Senior Explorer
ember-rank-corps-e5-short = SXPL
ember-rank-corps-e7-name = Chief Explorer
ember-rank-corps-e7-short = CXPL
ember-rank-corps-o1-name = Ensign
ember-rank-corps-o1-short = ENS
ember-rank-corps-o3-name = Lieutenant
ember-rank-corps-o3-short = LT
ember-rank-corps-o5-name = Commander
ember-rank-corps-o5-short = CDR
ember-rank-corps-o6-name = Captain
ember-rank-corps-o6-short = CAPT
ember-rank-corps-o8-name = Admiral
ember-rank-corps-o8-short = ADM
ember-rank-corps-o10-name = Commandant of the Expeditionary Corps
ember-rank-corps-o10-short = CMDT

## Government and Federal Police

ember-rank-scg-representative-name = SolGov Representative
ember-rank-scg-representative-short = SGR
ember-rank-scg-scientist-name = Government Scientist
ember-rank-federal-marshal-name = Federal Marshal
ember-rank-federal-marshal-short = MRSH
ember-rank-federal-agent-name = Federal Agent
ember-rank-federal-agent-short = AGT
ember-rank-federal-supervisor-name = Supervising Agent
ember-rank-federal-supervisor-short = SAGT

## Civilians and machines

ember-rank-civilian-name = Civilian
ember-rank-contractor-name = Contractor
ember-rank-machine-assigned-name = Assigned Unit
ember-rank-machine-assigned-short = UNIT

## Job requirements

character-branch-requirement = You must{$inverted ->
    [true]{" "}not
    *[other]{""}
} serve in one of these: {$branches}

character-rank-requirement = You must{$inverted ->
    [true]{" "}not
    *[other]{""}
} hold one of these ranks: {$ranks}

character-skill-requirement = You must{$inverted ->
    [true]{" "}not
    *[other]{""}
} be qualified in: {$skills}
character-skill-requirement-entry = {$skill} ({$level})

## Character editor

humanoid-profile-editor-branch-label = Branch
humanoid-profile-editor-rank-label = Rank
humanoid-profile-editor-branch-none = Unaffiliated
humanoid-profile-editor-rank-none = None
