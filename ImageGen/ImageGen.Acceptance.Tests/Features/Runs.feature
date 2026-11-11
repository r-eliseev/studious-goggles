@runs
Feature: Runs and generations

  @GEN-01 @S3.1 @S3.2
  Scenario: Two models receive the same seeds across two runs each
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                                              |
      | generate_images | A mountain cabin beneath an aurora in a clear sky. |
    And the seed source returns:
      | seed |
      | 11   |
      | 22   |
    When the user creates a new session
    And the user selects these image models with two runs per model:
      | model id                         |
      | black-forest-labs/flux.2-klein-4b |
      | vendor/beta                      |
    And the user sends the message "Draw an aurora over a cabin"
    Then the run shows four generation cards with seeds 11 and 22 for each model
