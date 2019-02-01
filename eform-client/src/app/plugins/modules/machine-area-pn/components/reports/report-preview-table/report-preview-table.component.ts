import {Component, Input, OnInit} from '@angular/core';
import {ReportPnFullModel} from 'src/app/plugins/modules/machine-area-pn/models/report';

@Component({
  selector: 'app-machine-area-pn-report-preview-table',
  templateUrl: './report-preview-table.component.html',
  styleUrls: ['./report-preview-table.component.scss']
})
export class ReportPreviewTableComponent implements OnInit {
  @Input() reportData: ReportPnFullModel = new ReportPnFullModel();
  constructor() { }

  ngOnInit() {
  }

}