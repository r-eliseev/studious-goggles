@actions
Feature: Generation card actions

  @ACT-01 @S4.1 @S4.4
  Scenario: Repeating a completed generation keeps its prompt, model, and seed
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                                     |
      | generate_images | A red fox sleeping beneath a birch tree.   |
    When the user creates a new session
    And the user sends the message "Draw a sleeping fox"
    Then the generation for model "black-forest-labs/flux.2-klein-4b" has finished
    When the user repeats the completed generation for model "black-forest-labs/flux.2-klein-4b"
    Then the timeline has two completed runs for model "black-forest-labs/flux.2-klein-4b"
    And both generations have the same prompt, model, and seed
    And the assistant was called once

  @ACT-04 @S4.5
  Scenario: Selecting another model for a failed generation keeps its prompt
    Given the OpenRouter image model catalog:
      | id                                 | name   | seed | price per MP |
      | black-forest-labs/flux.2-klein-4b | Klein  | true | 0.014        |
      | vendor/alpha                       | Alpha  | true | 0.02         |
      | vendor/beta                        | Beta   | true | 0.03         |
    And the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt               |
      | generate_images | A silver fox at dusk. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response | status | body                                |
      | error    | 500    | {"error":{"message":"failure"}} |
    When the user creates a new session
    And the user selects image models for this run:
      | model id                         |
      | black-forest-labs/flux.2-klein-4b |
      | vendor/alpha                     |
    And the user sends the message "Draw a silver fox"
    Then the generation for model "black-forest-labs/flux.2-klein-4b" has failed
    When the user opens other models for the failed generation
    Then model "vendor/alpha" is first in the used models
    When the user chooses model "vendor/beta" in the picker
    Then the timeline has two runs and the latest model is "vendor/beta"
    And the latest generation prompt is "A silver fox at dusk."
    And the assistant was called once

  @ACT-02 @S4.2 @S4.4
  Scenario: Requesting a new seed keeps the prompt and model
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                                     |
      | generate_images | A red fox sleeping beneath a birch tree.   |
    And the seed source returns:
      | seed |
      | 11   |
      | 22   |
    When the user creates a new session
    And the user sends the message "Draw a sleeping fox"
    Then the generation for model "black-forest-labs/flux.2-klein-4b" has finished
    When the user requests a new seed for model "black-forest-labs/flux.2-klein-4b"
    Then the timeline has two completed runs for model "black-forest-labs/flux.2-klein-4b"
    And both generations have the same prompt and model but different seeds
    And the assistant was called once

  @ACT-03 @S4.3
  Scenario: Editing a generation prompt starts a run without the assistant
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                   |
      | generate_images | A red fox in a forest.   |
    When the user creates a new session
    And the user sends the message "Draw a fox"
    Then the generation for model "black-forest-labs/flux.2-klein-4b" has finished
    When the edit prompt dialog will return "A blue fox beside a river."
    And the user edits the completed generation for model "black-forest-labs/flux.2-klein-4b"
    Then the timeline has two completed runs for model "black-forest-labs/flux.2-klein-4b"
    And the latest generation prompt is "A blue fox beside a river."
    And the assistant was called once

  @ACT-05 @S4.6
  Scenario: Copying a generation prompt puts the exact text on the clipboard
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                         |
      | generate_images | Keep every punctuation mark!   |
    When the user creates a new session
    And the user sends the message "Draw something"
    Then the generation for model "black-forest-labs/flux.2-klein-4b" has finished
    When the user copies the prompt for model "black-forest-labs/flux.2-klein-4b"
    Then the clipboard contains "Keep every punctuation mark!"

  @ACT-06 @S4.7
  Scenario: Details on a failed generation shows the provider error
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt              |
      | generate_images | A fox under a tree. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response | status | body                                                  |
      | error    | 500    | {"error":{"message":"specific provider failure"}} |
    When the user creates a new session
    And the user sends the message "Draw a fox"
    Then the generation for model "black-forest-labs/flux.2-klein-4b" has failed
    When the user opens error details for model "black-forest-labs/flux.2-klein-4b"
    Then the error dialog contains "specific provider failure"
    And the failed generation shows a short error reason

  @ACT-07 @F6.4
  Scenario: Saving a completed generation suggests a filename based on its id
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt               |
      | generate_images | A fox in the snow.   |
    When the user creates a new session
    And the user sends the message "Draw a fox"
    Then the generation for model "black-forest-labs/flux.2-klein-4b" has finished
    When the user saves the image for model "black-forest-labs/flux.2-klein-4b"
    Then the save dialog suggests the generation filename

  @ACT-08 @S4.5 @S4.6 @S4.7 @S4.8
  Scenario Outline: The actions menu reflects the generation state
    Given the user has a saved OpenRouter key
    When the user creates a new session
    And the user has a generation card in state "<state>"
    When the user opens the generation actions menu
    Then the actions menu matches state "<state>"

    Examples:
      | state     |
      | done      |
      | failed    |
      | filtered  |
      | cancelled |
