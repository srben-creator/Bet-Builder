"""
Cross-source team name normalization.
football-data.co.uk and Understat often use different spellings for the same team.
This dictionary maps football-data names to our clean, canonical names.
"""

FD_TO_CANONICAL = {
    # == EPL ==
    "Man United": "Manchester United",
    "Man City": "Manchester City",
    "Nott'm Forest": "Nottingham Forest",
    "Newcastle": "Newcastle United",
    "Tottenham": "Tottenham Hotspur",
    "Wolves": "Wolverhampton Wanderers",
    "Sheffield United": "Sheffield Utd",
    "Luton": "Luton Town",
    
    # == La Liga ==
    "Ath Madrid": "Atletico Madrid",
    "Ath Bilbao": "Athletic Club",
    "Espanol": "Espanyol",
    "Vallecano": "Rayo Vallecano",
    "Celta": "Celta Vigo",
    "Sociedad": "Real Sociedad",
    "Cadiz": "Cadiz CF",
    "Almeria": "UD Almeria",
    
    # == Bundesliga ==
    "Bayern Munich": "Bayern Munich",
    "B. Monchengladbach": "Borussia Monchengladbach",
    "Ein Frankfurt": "Eintracht Frankfurt",
    "Leverkusen": "Bayer Leverkusen",
    "Stuttgart": "VfB Stuttgart",
    "Heidenheim": "FC Heidenheim",
    
    # == Serie A ==
    "Milan": "AC Milan",
    "Inter": "Inter Milan",
    "Roma": "AS Roma",
    "Verona": "Hellas Verona",
    "Salernitana": "US Salernitana",
    
    # == Ligue 1 ==
    "Paris SG": "Paris Saint Germain",
    "Strasbourg": "RC Strasbourg",
    "Clermont": "Clermont Foot",
    
    # == Primeira Liga (Portugal) ==
    "Sp Lisbon": "Sporting CP",
    "Sp Braga": "SC Braga",
    "Estrela": "Estrela da Amadora",
    "Famalicao": "FC Famalicao",
    "Portimonense": "Portimonense SC",
    "Gil Vicente": "Gil Vicente FC",
    "Guimaraes": "Vitoria Guimaraes"
}

def get_canonical_name(fd_name: str) -> str:
    """Returns the canonical name if mapped, else returns the original fd_name."""
    # Strip any trailing/leading whitespaces from the raw data just in case
    clean_name = fd_name.strip()
    return FD_TO_CANONICAL.get(clean_name, clean_name)

FBREF_TO_CANONICAL = {
    # == EPL ==
    "Manchester Utd": "Manchester United",
    "Nott'ham Forest": "Nottingham Forest",
    "Newcastle Utd": "Newcastle United",
    "Tottenham": "Tottenham Hotspur",
    "Wolves": "Wolverhampton Wanderers",
    "Sheffield Utd": "Sheffield Utd",
    
    # == La Liga ==
    "Atlético Madrid": "Atletico Madrid",
    "Athletic Club": "Athletic Club",
    "Betis": "Real Betis",
    "Rayo Vallecano": "Rayo Vallecano",
    "Celta Vigo": "Celta Vigo",
    "Real Sociedad": "Real Sociedad",
    "Cádiz": "Cadiz CF",
    "Almería": "UD Almeria",
    "Alavés": "Alaves",
    
    # == Bundesliga ==
    "M'Gladbach": "Borussia Monchengladbach",
    "Eint Frankfurt": "Eintracht Frankfurt",
    "Leverkusen": "Bayer Leverkusen",
    
    # == Serie A ==
    "Milan": "AC Milan",
    "Inter": "Inter Milan",
    "Roma": "AS Roma",
    "Verona": "Hellas Verona",
    "Salernitana": "US Salernitana",
    
    # == Ligue 1 ==
    "Paris S-G": "Paris Saint Germain",
    "Strasbourg": "RC Strasbourg",
    "Clermont Foot": "Clermont Foot",
    
    # == Primeira Liga ==
    "Sporting CP": "Sporting CP",
    "Braga": "SC Braga",
    "Estrela": "Estrela da Amadora",
    "Famalicão": "FC Famalicao",
    "Portimonense": "Portimonense SC",
    "Vitória": "Vitoria Guimaraes"
}

def get_canonical_name_fbref(f_name: str) -> str:
    """Returns the canonical name if mapped, else returns the original FBref name."""
    clean_name = f_name.strip()
    return FBREF_TO_CANONICAL.get(clean_name, clean_name)
