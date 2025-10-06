Feature: Core flow example
  Scenario: Skeleton runs
    Given the repository is cloned
    When I execute the run command
    Then I see a successful start signal
