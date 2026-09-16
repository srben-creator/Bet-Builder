import numpy as np
from scipy.stats import poisson
from scipy.optimize import minimize
import pandas as pd

class DixonColesMathEngine:
    """
    Hybrid xG-Poisson Math Engine:
    1. Calculates Team Strengths (Attack/Defense) by fitting against historical xG (Expected Goals).
    2. Converts predicted lambdas into exact score probabilities using Dixon-Coles Bivariate Poisson.
    """
    
    def __init__(self, decay_rate=0.0065):
        # 0.0065 gives a half-life of roughly 100 days (depending on frequency)
        self.decay_rate = decay_rate
        self.teams = []
        self.team_to_idx = {}
        self.home_advantage = 1.0
        self.rho = 0.0
        
        # Will store the optimized arrays
        self.attack_strengths = None
        self.defense_strengths = None

    def _rho_correction(self, x, y, lambda_x, lambda_y, rho):
        """
        Dixon-Coles adjustment for low-scoring matches.
        Increases the probability of 0-0, 1-0, 0-1, 1-1 draws.
        """
        if x == 0 and y == 0:
            return 1 - lambda_x * lambda_y * rho
        elif x == 0 and y == 1:
            return 1 + lambda_x * rho
        elif x == 1 and y == 0:
            return 1 + lambda_y * rho
        elif x == 1 and y == 1:
            return 1 - rho
        else:
            return 1.0

    def generate_score_matrix(self, lambda_home, lambda_away, rho=0.0, max_goals=9):
        """
        Generates a matrix of exact score probabilities (e.g. matrix[1][0] is prob of 1-0)
        """
        matrix = np.zeros((max_goals + 1, max_goals + 1))
        
        for h in range(max_goals + 1):
            for a in range(max_goals + 1):
                # Standard Independent Poisson
                prob = poisson.pmf(h, lambda_home) * poisson.pmf(a, lambda_away)
                # Apply Dixon-Coles adjustment
                correction = self._rho_correction(h, a, lambda_home, lambda_away, rho)
                
                matrix[h][a] = max(0, prob * correction)
                
        # Normalize just in case the correction pushed the sum slightly off 1.0
        return matrix / np.sum(matrix)

    def extract_market_probabilities(self, score_matrix):
        """
        Derives all betting markets from the 2D score matrix.
        """
        home_win = np.sum(np.tril(score_matrix, -1))
        draw = np.sum(np.diag(score_matrix))
        away_win = np.sum(np.triu(score_matrix, 1))
        
        # Over/Under 2.5
        under_25 = 0.0
        for h in range(score_matrix.shape[0]):
            for a in range(score_matrix.shape[1]):
                if h + a < 2.5:
                    under_25 += score_matrix[h][a]
        over_25 = 1.0 - under_25
        
        # Over/Under 1.5
        under_15 = 0.0
        for h in range(score_matrix.shape[0]):
            for a in range(score_matrix.shape[1]):
                if h + a < 1.5:
                    under_15 += score_matrix[h][a]
        over_15 = 1.0 - under_15
        
        # Double Chance
        dc_1x = home_win + draw
        dc_12 = home_win + away_win
        dc_x2 = draw + away_win
        
        return {
            "1X2": {"H": home_win, "D": draw, "A": away_win},
            "OU25": {"Over": over_25, "Under": under_25},
            "OU15": {"Over": over_15, "Under": under_15},
            "DC": {"1X": dc_1x, "12": dc_12, "X2": dc_x2}
        }

    def _loss_function(self, params, home_idx, away_idx, home_xg, away_xg, weights):
        """
        Least-squares loss function to fit Attack/Defense ratings against historical xG.
        """
        num_teams = len(self.teams)
        
        # Unpack parameters
        attacks = params[:num_teams]
        defenses = params[num_teams:2*num_teams]
        home_adv = params[-1]
        
        # Predict xG
        pred_home_xg = attacks[home_idx] * defenses[away_idx] * home_adv
        pred_away_xg = attacks[away_idx] * defenses[home_idx]
        
        # Calculate Weighted Squared Error
        error_home = ((pred_home_xg - home_xg) ** 2) * weights
        error_away = ((pred_away_xg - away_xg) ** 2) * weights
        
        return np.sum(error_home + error_away)

    def fit(self, df):
        """
        Expects a pandas DataFrame with:
        home_team, away_team, home_xg, away_xg, days_ago
        """
        self.teams = sorted(list(set(df["home_team"].unique()) | set(df["away_team"].unique())))
        self.team_to_idx = {team: i for i, team in enumerate(self.teams)}
        num_teams = len(self.teams)
        
        home_idx = df["home_team"].map(self.team_to_idx).values
        away_idx = df["away_team"].map(self.team_to_idx).values
        home_xg = df["home_xg"].values
        away_xg = df["away_xg"].values
        
        # Calculate time decay weights
        weights = np.exp(-self.decay_rate * df["days_ago"].values)
        
        # Initial guess: Average attack and defense is 1.0, Home advantage is 1.1
        initial_params = np.ones(num_teams * 2 + 1)
        initial_params[-1] = 1.1 
        
        # Bounds: Ratings must be > 0
        bounds = [(0.1, 5.0)] * (num_teams * 2) + [(0.5, 2.0)]
        
        # Constraint: Average attack rating must equal 1.0 (to prevent parameters from drifting to infinity)
        def constraint_avg_attack(params):
            return np.mean(params[:num_teams]) - 1.0
            
        constraints = [{'type': 'eq', 'fun': constraint_avg_attack}]
        
        print(f"Fitting model for {num_teams} teams over {len(df)} matches...")
        
        res = minimize(
            self._loss_function,
            initial_params,
            args=(home_idx, away_idx, home_xg, away_xg, weights),
            method='SLSQP',
            bounds=bounds,
            constraints=constraints,
            options={'maxiter': 500}
        )
        
        if not res.success:
            print(f"[!] Warning: Model optimization failed: {res.message}")
            
        self.attack_strengths = res.x[:num_teams]
        self.defense_strengths = res.x[num_teams:2*num_teams]
        self.home_advantage = res.x[-1]
        
        # Estimate Rho based on standard football distributions 
        # (usually around -0.1 to -0.15 for low scoring sports to inflate draws)
        self.rho = -0.13 
        
        print(f"Model fit successful! Home Advantage: {self.home_advantage:.2f}")

    def predict_match(self, home_team, away_team):
        """
        Predicts Expected Goals and returns market probabilities.
        """
        if home_team not in self.team_to_idx or away_team not in self.team_to_idx:
            raise ValueError(f"Team not found in model training data.")
            
        h_idx = self.team_to_idx[home_team]
        a_idx = self.team_to_idx[away_team]
        
        pred_home_xg = self.attack_strengths[h_idx] * self.defense_strengths[a_idx] * self.home_advantage
        pred_away_xg = self.attack_strengths[a_idx] * self.defense_strengths[h_idx]
        
        # Generate score matrix using predicted xG as the Poisson Lambdas
        matrix = self.generate_score_matrix(pred_home_xg, pred_away_xg, self.rho)
        probs = self.extract_market_probabilities(matrix)
        
        return {
            "predicted_home_xg": pred_home_xg,
            "predicted_away_xg": pred_away_xg,
            "probabilities": probs
        }
