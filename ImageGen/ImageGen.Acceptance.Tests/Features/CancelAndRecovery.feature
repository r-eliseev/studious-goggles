@cancel
Feature: Cancellation and recovery

  @CAN-01 @S3.5.1 @F5.4 @ignore @bug-287
  Scenario: Cancelling one active generation leaves the other running
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                          |
      | generate_images | A fox and an owl in a forest. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response |
      | hold     |
    And the provider answers generation for model "vendor/beta" with:
      | response |
      | hold     |
    When the user creates a new session
    And the user selects image models for this run:
      | model id                         |
      | black-forest-labs/flux.2-klein-4b |
      | vendor/beta                      |
    And the user sends the message "Draw a fox and owl"
    Then model "black-forest-labs/flux.2-klein-4b" has an active generation card
    And model "vendor/beta" has an active generation card
    And the provider has received the generation request for model "black-forest-labs/flux.2-klein-4b"
    And the provider has received the generation request for model "vendor/beta"
    When the user cancels the generation for model "black-forest-labs/flux.2-klein-4b"
    Then the generation for model "black-forest-labs/flux.2-klein-4b" is cancelled with stopped-waiting status
    When the provider completes generation for model "vendor/beta"
    Then model "vendor/beta" has a done generation card with an image

  @CAN-03 @S3.5.2
  Scenario: A result that arrives before cancellation stays done
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                       |
      | generate_images | A fox beside a mountain lake. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response | width | height | cost |
      | picture  | 64    | 64     | 0.01 |
    When the user creates a new session
    And the user sends the message "Draw a fox by a lake"
    Then model "black-forest-labs/flux.2-klein-4b" has a done generation card with an image
    When the user cancels the generation for model "black-forest-labs/flux.2-klein-4b"
    Then the generated image is saved before the card shows it

  @CAN-04 @S3.8
  Scenario: An unfinished generation is marked interrupted after restart
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                   |
      | generate_images | A red fox in a snowstorm. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response |
      | hold     |
    When the user creates a new session
    And the user sends the message "Draw a fox in a snowstorm"
    Then model "black-forest-labs/flux.2-klein-4b" has an active generation card
    When the user restarts the app while a generation is active
    Then the user opens the generated image session
    And the generation for model "black-forest-labs/flux.2-klein-4b" has failed with reason "The app closed before the generation finished."

  @CAN-02 @S3.5.1 @S3.5.2
  Scenario: Cancelling a run leaves its completed generation unchanged
    Given the OpenRouter image model catalog:
      | id                                | name       | seed | price per MP |
      | black-forest-labs/flux.2-klein-4b | Flux Klein | true | 0.014        |
      | vendor/beta                       | Beta       | true | 0.02         |
      | vendor/gamma                     | Gamma      | true | 0.03         |
    And the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                       |
      | generate_images | Three views of a red fox.     |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response | width | height | cost |
      | picture  | 64    | 64     | 0.01 |
    And the provider answers generation for model "vendor/beta" with:
      | response |
      | hold     |
    And the provider answers generation for model "vendor/gamma" with:
      | response |
      | hold     |
    When the user creates a new session
    And the user selects image models for this run:
      | model id                         |
      | black-forest-labs/flux.2-klein-4b |
      | vendor/beta                      |
      | vendor/gamma                     |
    And the user sends the message "Draw three fox views"
    Then model "black-forest-labs/flux.2-klein-4b" has a done generation card with an image
    And model "vendor/beta" has an active generation card
    And model "vendor/gamma" has an active generation card
    When the user cancels the current run
    Then generation cards for models "vendor/beta" and "vendor/gamma" are cancelled
    And model "black-forest-labs/flux.2-klein-4b" has a done generation card with an image

  @CAN-05 @S3.5.3 @ignore @bug-287
  Scenario: The cancellation status remains visible after restart
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                         |
      | generate_images | A fox waiting by a riverbank. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response |
      | hold     |
    When the user creates a new session
    And the user sends the message "Draw a fox by a river"
    Then model "black-forest-labs/flux.2-klein-4b" has an active generation card
    When the user cancels the generation for model "black-forest-labs/flux.2-klein-4b"
    Then the generation for model "black-forest-labs/flux.2-klein-4b" is cancelled with stopped-waiting status
    When the user restarts the app
    Then the user opens the generated image session
    And the generation for model "black-forest-labs/flux.2-klein-4b" is cancelled with stopped-waiting status
