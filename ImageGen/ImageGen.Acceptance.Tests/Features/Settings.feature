@settings
Feature: Settings and appearance

  @SET-01 @S6.1 @S6.2
  Scenario: The default appearance is Quiet with the system theme
    Given the user has a saved OpenRouter key
    When the user opens settings
    Then the saved appearance is the default

  @SET-02 @S6.3
  Scenario: Changing the appearance applies immediately and persists after restart
    Given the user has a saved OpenRouter key
    When the user opens settings
    And the user chooses the Gallery style and dark theme
    Then the active appearance is Gallery with the dark theme
    When the user restarts the app
    Then the active appearance is Gallery with the dark theme

  @SET-03 @S6.4
  Scenario: Settings opens from the sessions screen
    Given the user has a saved OpenRouter key
    When the user opens settings from the session list
    Then the Settings route is open

  @SET-04
  Scenario: Clearing logs removes old files and logging continues
    Given the user has a saved OpenRouter key
    And an old log file exists
    When the user opens settings
    And log clearing is confirmed
    And the user clears logs
    Then old log files are deleted
    And the app writes another log entry

